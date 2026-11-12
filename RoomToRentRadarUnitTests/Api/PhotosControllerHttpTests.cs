using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Api;

// uploadPhoto/deletePhoto — UC-30, BR-MED-01..03.
// NonParallelizable: PhotoApiFactory overrides process-wide environment variables.
[TestFixture]
[NonParallelizable]
public class PhotosControllerHttpTests
{
    private const string SigningKey = "0123456789abcdef0123456789abcdef";
    private const string Issuer = "RoomToRentRadar.Api";
    private const string Audience = "RoomToRentRadar.Client";
    private const string PhotosUrl = "/api/landlord/photos";

    // 11-byte JPEG SOF0 frame: precision 8, height 600, width 800. Enough for ImageHeaderReader.
    private static readonly byte[] MinimalJpeg =
    {
        0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x02, 0x58, 0x03, 0x20
    };

    // PNG signature + IHDR chunk header + width 800 + height 600 (24 bytes).
    private static readonly byte[] MinimalPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x03, 0x20,
        0x00, 0x00, 0x02, 0x58
    };

    private PhotoApiFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new PhotoApiFactory();
        try
        {
            _client = _factory.CreateClient();
        }
        catch
        {
            _factory.Dispose();
            throw;
        }
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();

        try
        {
            if (Directory.Exists(_factory.MediaRoot))
            {
                Directory.Delete(_factory.MediaRoot, recursive: true);
            }
        }
        catch
        {
            // Best effort: a locked temp file must not fail the test run.
        }
    }

    [Test]
    public async Task Upload_WithoutToken_Returns401()
    {
        using var content = CreateUploadContent(MinimalJpeg, "room.jpg", "Standard");
        using var response = await _client.PostAsync(PhotosUrl, content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Upload_AsStudent_Returns403()
    {
        var token = CreateToken("student-1", "Student", isVerified: true);

        using var response = await UploadAsync(MinimalJpeg, "room.jpg", "Standard", token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Upload_AsUnverifiedLandlord_Returns403()
    {
        var token = CreateToken("landlord-1", "Landlord", isVerified: false);

        using var response = await UploadAsync(MinimalJpeg, "room.jpg", "Standard", token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Upload_ValidJpeg_Returns201LocationAndServesMedia()
    {
        var token = CreateToken("landlord-1", "Landlord", isVerified: true);

        var (json, location) = await UploadCreatedAsync(token, MinimalJpeg, "room.jpg", "Standard");
        using (json)
        {
            var root = json.RootElement;
            var id = root.GetProperty("id").GetString();
            var kind = root.GetProperty("kind").GetString();
            var thumbUrl = root.GetProperty("thumbUrl").GetString();
            var mediumUrl = root.GetProperty("mediumUrl").GetString();

            Assert.Multiple(() =>
            {
                Assert.That(id, Is.Not.Null.And.Matches("^[0-9a-f]{32}$"));
                Assert.That(kind, Is.EqualTo("Standard"));
                Assert.That(thumbUrl, Is.Not.Null.And.EqualTo(mediumUrl));
                Assert.That(thumbUrl, Does.StartWith("http://localhost/media/"));
                Assert.That(root.GetProperty("width").GetInt32(), Is.EqualTo(800));
                Assert.That(root.GetProperty("height").GetInt32(), Is.EqualTo(600));
                Assert.That(root.GetProperty("sizeBytes").GetInt64(), Is.EqualTo(MinimalJpeg.Length));
                Assert.That(location, Is.Not.Null.And.Contains("/media/"));
            });

            using var mediaResponse = await _client.GetAsync(thumbUrl);
            Assert.That(mediaResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await mediaResponse.Content.ReadAsByteArrayAsync(), Is.EqualTo(MinimalJpeg));
            Assert.That(mediaResponse.Headers.TryGetValues("Cache-Control", out var cacheControl), Is.True);
            Assert.That(string.Join(",", cacheControl!), Does.Contain("immutable"));
            Assert.That(mediaResponse.Headers.TryGetValues("X-Content-Type-Options", out var nosniff), Is.True);
            Assert.That(nosniff!.Single(), Is.EqualTo("nosniff"));
        }
    }

    [Test]
    public async Task Upload_ValidPng_Returns201()
    {
        var token = CreateToken("landlord-1", "Landlord", isVerified: true);

        var (json, _) = await UploadCreatedAsync(token, MinimalPng, "room.png", "Standard", "image/png");
        using (json)
        {
            Assert.That(json.RootElement.GetProperty("kind").GetString(), Is.EqualTo("Standard"));
            Assert.That(json.RootElement.GetProperty("id").GetString(), Is.Not.Null.And.Matches("^[0-9a-f]{32}$"));
        }
    }

    [Test]
    public async Task Upload_GarbageBytes_Returns415()
    {
        var token = CreateToken("landlord-1", "Landlord", isVerified: true);

        using var response = await UploadAsync(new byte[16], "garbage.jpg", "Standard", token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnsupportedMediaType));
        await AssertProblemBodyAsync(response, HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");
    }

    [Test]
    public async Task Upload_OversizedFile_Returns413()
    {
        var token = CreateToken("landlord-1", "Landlord", isVerified: true);
        var oversized = new byte[10 * 1024 * 1024 + 1];
        MinimalJpeg.CopyTo(oversized, 0);

        using var response = await UploadAsync(oversized, "huge.jpg", "Standard", token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
        await AssertProblemBodyAsync(response, HttpStatusCode.RequestEntityTooLarge, "FILE_TOO_LARGE");
    }

    [Test]
    public async Task Upload_MissingFile_Returns400()
    {
        var token = CreateToken("landlord-1", "Landlord", isVerified: true);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Standard"), "kind");
        using var request = new HttpRequestMessage(HttpMethod.Post, PhotosUrl) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        await AssertProblemBodyAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Test]
    public async Task Upload_MissingKind_Returns400()
    {
        var token = CreateToken("landlord-1", "Landlord", isVerified: true);
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(MinimalJpeg);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(file, "file", "room.jpg");
        using var request = new HttpRequestMessage(HttpMethod.Post, PhotosUrl) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        await AssertProblemBodyAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.That(json.RootElement.GetProperty("detail").GetString(), Is.EqualTo("Photo kind is required."), body);
    }

    [Test]
    public async Task Upload_InvalidKind_Returns400()
    {
        var token = CreateToken("landlord-1", "Landlord", isVerified: true);

        using var response = await UploadAsync(MinimalJpeg, "room.jpg", "NotAKind", token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        await AssertProblemBodyAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Test]
    public async Task Upload_PanoramaWrongRatio_Returns400()
    {
        var token = CreateToken("landlord-1", "Landlord", isVerified: true);

        using var response = await UploadAsync(MinimalJpeg, "room.jpg", "Panorama", token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        await AssertProblemBodyAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Test]
    public async Task Upload_LowercaseStandardKind_Returns201()
    {
        var token = CreateToken("landlord-1", "Landlord", isVerified: true);

        // The former [FromForm] PhotoKind? binder was case-insensitive, so "standard" returned 201.
        // A case-sensitive parse must not narrow that previously-accepted input to a 400.
        var (json, location) = await UploadCreatedAsync(token, MinimalJpeg, "room.jpg", "standard");
        using (json)
        {
            Assert.Multiple(() =>
            {
                Assert.That(json.RootElement.GetProperty("kind").GetString(), Is.EqualTo("Standard"));
                Assert.That(json.RootElement.GetProperty("id").GetString(), Is.Not.Null.And.Matches("^[0-9a-f]{32}$"));
                Assert.That(location, Is.Not.Null.And.Contains("/media/"));
            });
        }
    }

    [Test]
    public async Task Upload_LowercasePanoramaKind_Returns400WithRatioDetail()
    {
        var token = CreateToken("landlord-1", "Landlord", isVerified: true);

        using var response = await UploadAsync(MinimalJpeg, "room.jpg", "panorama", token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        await AssertProblemBodyAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");

        // The ratio message proves "panorama" parsed as Panorama and reached UploadPhotoUseCase,
        // rather than being rejected here as an invalid kind.
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.That(
            json.RootElement.GetProperty("detail").GetString(),
            Does.Contain("Panorama photos must use a 2:1 aspect ratio"),
            body);
    }

    [Test]
    public async Task Upload_UndefinedNumericKind_Returns400()
    {
        var token = CreateToken("landlord-1", "Landlord", isVerified: true);

        // Enum.TryParse also accepts numeric strings, so Enum.IsDefined must still reject 99.
        using var response = await UploadAsync(MinimalJpeg, "room.jpg", "99", token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        await AssertProblemBodyAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.That(json.RootElement.GetProperty("detail").GetString(), Is.EqualTo("Photo kind is invalid."), body);
    }

    [Test]
    public async Task Delete_OtherUsersPhoto_Returns404()
    {
        var ownerToken = CreateToken("landlord-a", "Landlord", isVerified: true);
        var (photoId, _) = await UploadAndGetUrlsAsync(ownerToken);

        var otherToken = CreateToken("landlord-b", "Landlord", isVerified: true);
        using var response = await DeleteAsync(photoId, otherToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        await AssertProblemBodyAsync(response, HttpStatusCode.NotFound, "PHOTO_NOT_FOUND");
    }

    [Test]
    public async Task Delete_MissingPhoto_Returns404()
    {
        var token = CreateToken("landlord-a", "Landlord", isVerified: true);

        // A real 32-hex id that was never uploaded: the not-found branch must be indistinguishable
        // from the cross-owner case (anti-IDOR) — same 404, same errorCode.
        using var response = await DeleteAsync("0123456789abcdef0123456789abcdef", token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        await AssertProblemBodyAsync(response, HttpStatusCode.NotFound, "PHOTO_NOT_FOUND");
    }

    [Test]
    public async Task Delete_AttachedPhoto_Returns409()
    {
        var token = CreateToken("landlord-a", "Landlord", isVerified: true);
        var (photoId, _) = await UploadAndGetUrlsAsync(token);

        using (var scope = _factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IRoomPhotoRepository>();
            var attached = await repository.AttachAsync("landlord-a", "room-1", new[] { photoId });
            Assert.That(attached, Is.EqualTo(1));
        }

        using var response = await DeleteAsync(photoId, token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        await AssertProblemBodyAsync(response, HttpStatusCode.Conflict, "PHOTO_ATTACHED");
    }

    [Test]
    public async Task Delete_OwnUnattachedPhoto_Returns204()
    {
        var token = CreateToken("landlord-a", "Landlord", isVerified: true);
        var (photoId, thumbUrl) = await UploadAndGetUrlsAsync(token);

        using (var response = await DeleteAsync(photoId, token))
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        }

        using var mediaResponse = await _client.GetAsync(thumbUrl);
        Assert.That(mediaResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_WithoutToken_Returns401()
    {
        using var response = await DeleteAsync("0123456789abcdef0123456789abcdef", token: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private static string CreateToken(string userId, string role, bool isVerified)
    {
        var claims = new[]
        {
            new Claim("sub", userId),
            new Claim("role", role),
            new Claim("is_verified", isVerified ? "true" : "false")
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static MultipartFormDataContent CreateUploadContent(
        byte[] bytes,
        string fileName,
        string kind,
        string contentType = "image/jpeg")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        content.Add(new StringContent(kind), "kind");
        return content;
    }

    private async Task<HttpResponseMessage> UploadAsync(
        byte[] bytes,
        string fileName,
        string kind,
        string? token,
        string contentType = "image/jpeg")
    {
        using var content = CreateUploadContent(bytes, fileName, kind, contentType);
        using var request = new HttpRequestMessage(HttpMethod.Post, PhotosUrl) { Content = content };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _client.SendAsync(request);
    }

    private async Task<(JsonDocument Json, string? Location)> UploadCreatedAsync(
        string token,
        byte[] bytes,
        string fileName,
        string kind,
        string contentType = "image/jpeg")
    {
        using var response = await UploadAsync(bytes, fileName, kind, token, contentType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), body);

        return (JsonDocument.Parse(body), response.Headers.Location?.ToString());
    }

    private async Task<(string Id, string ThumbUrl)> UploadAndGetUrlsAsync(string token)
    {
        var (json, _) = await UploadCreatedAsync(token, MinimalJpeg, "room.jpg", "Standard");
        using (json)
        {
            return (
                json.RootElement.GetProperty("id").GetString()!,
                json.RootElement.GetProperty("thumbUrl").GetString()!);
        }
    }

    private async Task<HttpResponseMessage> DeleteAsync(string photoId, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{PhotosUrl}/{photoId}");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _client.SendAsync(request);
    }

    // Asserts the application/problem+json body contract (ADR-012; openapi ProblemDetails
    // required: [title, status, errorCode]). Each test asserts the HTTP status separately.
    private static async Task AssertProblemBodyAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedErrorCode)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(
                response.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/problem+json"),
                body);

            // Assert the exact camelCase member name exists — do not assume it.
            Assert.That(root.TryGetProperty("errorCode", out var errorCode), Is.True, $"No camelCase 'errorCode'. Body: {body}");
            Assert.That(root.TryGetProperty("title", out var title), Is.True, body);
            Assert.That(root.TryGetProperty("status", out var status), Is.True, body);

            Assert.That(
                errorCode.ValueKind == JsonValueKind.String ? errorCode.GetString() : null,
                Is.EqualTo(expectedErrorCode),
                body);
            Assert.That(
                title.ValueKind == JsonValueKind.String ? title.GetString() : null,
                Is.Not.Null.And.Not.Empty,
                body);
            Assert.That(
                status.ValueKind == JsonValueKind.Number ? status.GetInt32() : -1,
                Is.EqualTo((int)expectedStatus),
                body);
        });
    }

    private sealed class PhotoApiFactory : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> _previousEnvironment = new(StringComparer.Ordinal);

        public PhotoApiFactory()
        {
            MediaRoot = Path.Combine(Path.GetTempPath(), "rtrr-http-tests", Guid.NewGuid().ToString("N"));

            // WebApplicationFactory's ConfigureAppConfiguration callbacks only run at Build(), but
            // Program.cs reads builder.Configuration eagerly while building the host, so the
            // overrides must be in the process environment ("Section__Key" maps to "Section:Key").
            SetEnvironmentVariable("Jwt__SigningKey", SigningKey);
            SetEnvironmentVariable("Jwt__Issuer", Issuer);
            SetEnvironmentVariable("Jwt__Audience", Audience);
            SetEnvironmentVariable("Sqlite__ConnectionString", "Data Source=:memory:");
            SetEnvironmentVariable("Media__RootPath", MediaRoot);
            SetEnvironmentVariable("Media__PublicBaseUrl", "http://localhost");
        }

        public string MediaRoot { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            foreach (var (name, value) in _previousEnvironment)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        private void SetEnvironmentVariable(string name, string value)
        {
            _previousEnvironment[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
