using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadarUnitTests.Application;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// QA / US08 end-to-end through the real HTTP pipeline and real adapters (Application use cases,
/// SQLite repositories, local-disk media store): upload photos → create listing → list → detail →
/// If-Match update. Proves the DI wiring that unit tests with substitutes cannot.
/// </summary>
[TestFixture]
public class LandlordListingFlowIntegrationTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";
    private const string Password = "StrongPass123";

    private string _mediaRoot = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), "rtrr-listing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_mediaRoot);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Sqlite:SeedDemoRooms", "false");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
            webHost.UseSetting("Media:RootPath", _mediaRoot);
            webHost.UseSetting("Media:PublicBaseUrl", "http://localhost:5000");
            webHost.UseSetting("Media:PhotoCleanupEnabled", "false");
        });
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();

        if (Directory.Exists(_mediaRoot))
        {
            Directory.Delete(_mediaRoot, recursive: true);
        }
    }

    [Test]
    public async Task ListingFlow_UploadCreateListDetailUpdate_WorksEndToEnd()
    {
        var token = await RegisterVerifiedLandlordAsync("flow-landlord@example.com");
        var photoIds = new List<string>();
        for (var index = 0; index < 3; index++)
        {
            photoIds.Add(await UploadPhotoAsync(token, AppTestData.Jpeg(1280, 720)));
        }

        var createResponse = await SendAsync(HttpMethod.Post, "/api/landlord/rooms", token, ListingPayload(photoIds));
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var eTag = createResponse.Headers.ETag?.Tag;
        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var createdRoot = created.RootElement;
        var roomId = createdRoot.GetProperty("id").GetString();

        Assert.Multiple(() =>
        {
            Assert.That(eTag, Is.EqualTo("\"1\""), "ADR-012: ETag = \"<version>\".");
            Assert.That(createdRoot.GetProperty("status").GetString(), Is.EqualTo("PendingReview"));
            Assert.That(createdRoot.GetProperty("version").GetInt32(), Is.EqualTo(1));
            Assert.That(createdRoot.GetProperty("photos").GetArrayLength(), Is.EqualTo(3));
            Assert.That(createdRoot.GetProperty("houseRules").GetProperty("versionNumber").GetInt32(), Is.EqualTo(1));
            Assert.That(createdRoot.GetProperty("listing").GetProperty("amenities").GetArrayLength(), Is.EqualTo(2));
        });

        var listResponse = await SendAsync(HttpMethod.Get, "/api/landlord/rooms", token);
        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var listRoot = list.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(listRoot.GetProperty("totalCount").GetInt32(), Is.EqualTo(1));
            Assert.That(listRoot.GetProperty("countsByStatus").GetProperty("PendingReview").GetInt32(), Is.EqualTo(1));
            Assert.That(listRoot.GetProperty("countsByStatus").GetProperty("Available").GetInt32(), Is.Zero);
            Assert.That(listRoot.GetProperty("items")[0].GetProperty("coverThumbUrl").GetString(),
                Does.StartWith("http://localhost:5000/media/rooms/"));
        });

        var detailResponse = await SendAsync(HttpMethod.Get, $"/api/landlord/rooms/{roomId}", token);
        Assert.That(detailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var reversed = photoIds.AsEnumerable().Reverse().ToList();
        var updateResponse = await SendAsync(
            HttpMethod.Put, $"/api/landlord/rooms/{roomId}", token, ListingPayload(reversed), ifMatch: eTag);
        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var updated = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        var updatedRoot = updated.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(updateResponse.Headers.ETag?.Tag, Is.EqualTo("\"2\""));
            Assert.That(updatedRoot.GetProperty("version").GetInt32(), Is.EqualTo(2));
            Assert.That(updatedRoot.GetProperty("needsReReview").GetBoolean(), Is.False,
                "Đổi thứ tự ảnh không phải thay đổi material (BR-LST-05).");
            Assert.That(
                updatedRoot.GetProperty("listing").GetProperty("photoIds").EnumerateArray().Select(item => item.GetString()),
                Is.EqualTo(reversed));
        });

        var missingIfMatch = await SendAsync(HttpMethod.Put, $"/api/landlord/rooms/{roomId}", token, ListingPayload(reversed));
        await AssertProblemAsync(missingIfMatch, HttpStatusCode.PreconditionRequired, "IF_MATCH_REQUIRED");

        var staleIfMatch = await SendAsync(
            HttpMethod.Put, $"/api/landlord/rooms/{roomId}", token, ListingPayload(reversed), ifMatch: "\"1\"");
        await AssertProblemAsync(staleIfMatch, HttpStatusCode.PreconditionFailed, "ETAG_MISMATCH");
    }

    [Test]
    public async Task CreateListing_WithTwoPhotos_Returns400ValidationFailed()
    {
        var token = await RegisterVerifiedLandlordAsync("two-photos@example.com");
        var photoIds = new[]
        {
            await UploadPhotoAsync(token, AppTestData.Jpeg()),
            await UploadPhotoAsync(token, AppTestData.Jpeg())
        };

        var response = await SendAsync(HttpMethod.Post, "/api/landlord/rooms", token, ListingPayload(photoIds));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Test]
    public async Task UploadPhoto_TextFile_Returns415UnsupportedMediaType()
    {
        var token = await RegisterVerifiedLandlordAsync("text-upload@example.com");

        var response = await UploadPhotoResponseAsync(token, "not-an-image"u8.ToArray(), "fake.jpg");

        await AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");
    }

    [Test]
    public async Task UploadPhoto_WithoutKindField_Returns400ValidationFailed()
    {
        // openapi uploadPhoto: requestBody required [file, kind]; BR-MED-02 lists a missing kind as
        // 400 VALIDATION_FAILED. Before the fix this silently stored the photo as Standard.
        var token = await RegisterVerifiedLandlordAsync("missing-kind@example.com");

        var response = await UploadPhotoResponseAsync(token, AppTestData.Jpeg(), "photo.jpg", kind: null);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Test]
    public async Task UploadPhoto_UnknownKindName_Returns400ValidationFailed()
    {
        var token = await RegisterVerifiedLandlordAsync("unknown-kind@example.com");

        var response = await UploadPhotoResponseAsync(token, AppTestData.Jpeg(), "photo.jpg", kind: "NotAPhotoKind");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Test]
    public async Task UploadPhoto_AboveTenMegabytes_Returns413FileTooLarge()
    {
        // BR-MED-02: <= 10,485,760 bytes; the 11 MB transport headroom lets one byte over the rule
        // reach the use case so the client receives 413 FILE_TOO_LARGE instead of a bare Kestrel error.
        var token = await RegisterVerifiedLandlordAsync("too-large@example.com");
        var oversize = new byte[10 * 1024 * 1024 + 1];
        AppTestData.Jpeg().CopyTo(oversize, 0);

        var response = await UploadPhotoResponseAsync(token, oversize, "huge.jpg");

        await AssertProblemAsync(response, HttpStatusCode.RequestEntityTooLarge, "FILE_TOO_LARGE");
    }

    [Test]
    public async Task UploadPhoto_AboveTransportLimit_Returns413FileTooLarge()
    {
        // Medium M2: a body above the 11 MB [RequestSizeLimit] is rejected by the host before the use
        // case runs; it must still be problem+json 413 FILE_TOO_LARGE (ADR-012 §3, openapi PayloadTooLarge).
        var token = await RegisterVerifiedLandlordAsync("above-transport-limit@example.com");
        var oversize = new byte[11 * 1024 * 1024 + 1];
        AppTestData.Jpeg().CopyTo(oversize, 0);

        var response = await UploadPhotoResponseAsync(token, oversize, "huge-11mb.jpg");

        await AssertProblemAsync(response, HttpStatusCode.RequestEntityTooLarge, "FILE_TOO_LARGE");
    }

    [Test]
    public async Task UploadPhoto_AsStudent_Returns403Forbidden()
    {
        var (studentToken, _) = await RegisterAsync("student-upload@example.com", "Student");

        var response = await UploadPhotoResponseAsync(studentToken, AppTestData.Jpeg(), "photo.jpg");

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "FORBIDDEN");
    }

    [Test]
    public async Task CreateListing_AsUnverifiedLandlord_Returns403Forbidden()
    {
        // BR-AUTH-03: an unverified landlord may sign in but cannot publish (VerifiedLandlordPolicy).
        var (token, _) = await RegisterAsync("unverified-landlord@example.com", "Landlord");

        var response = await SendAsync(
            HttpMethod.Post,
            "/api/landlord/rooms",
            token,
            ListingPayload(["photo-1", "photo-2", "photo-3"]));

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "FORBIDDEN");
    }

    [Test]
    public async Task ListMyListings_AsUnverifiedLandlord_Returns200()
    {
        // LandlordPolicy (list/get/edit/unpublish/mark) only needs the Landlord role, unlike publish.
        var (token, _) = await RegisterAsync("unverified-lister@example.com", "Landlord");

        var response = await SendAsync(HttpMethod.Get, "/api/landlord/rooms", token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GetMyListing_OfAnotherLandlord_Returns404RoomNotFound()
    {
        // GUARDRAILS 1.5.6 / BR-LST-07 (anti-IDOR): another owner's listing must look like it does not exist.
        var ownerToken = await RegisterVerifiedLandlordAsync("owner-404@example.com");
        var photoIds = new List<string>();
        for (var index = 0; index < 3; index++)
        {
            photoIds.Add(await UploadPhotoAsync(ownerToken, AppTestData.Jpeg()));
        }

        var createResponse = await SendAsync(HttpMethod.Post, "/api/landlord/rooms", ownerToken, ListingPayload(photoIds));
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var roomId = created.RootElement.GetProperty("id").GetString();

        var otherLandlordToken = await RegisterVerifiedLandlordAsync("intruder-404@example.com");
        var response = await SendAsync(HttpMethod.Get, $"/api/landlord/rooms/{roomId}", otherLandlordToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "ROOM_NOT_FOUND");
    }

    [Test]
    public async Task DeletePhoto_WhenAttached_Returns409InvalidStateTransition()
    {
        var token = await RegisterVerifiedLandlordAsync("delete-attached@example.com");
        var photoIds = new List<string>();
        for (var index = 0; index < 3; index++)
        {
            photoIds.Add(await UploadPhotoAsync(token, AppTestData.Jpeg()));
        }

        var createResponse = await SendAsync(HttpMethod.Post, "/api/landlord/rooms", token, ListingPayload(photoIds));
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var deleteResponse = await SendAsync(HttpMethod.Delete, $"/api/landlord/photos/{photoIds[0]}", token);

        await AssertProblemAsync(deleteResponse, HttpStatusCode.Conflict, "INVALID_STATE_TRANSITION");
    }

    [Test]
    public async Task ListingEndpoints_AsStudent_Return403Forbidden()
    {
        var (studentToken, _) = await RegisterAsync("student-flow@example.com", "Student");

        var response = await SendAsync(HttpMethod.Get, "/api/landlord/rooms", studentToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "FORBIDDEN");
    }

    // ----------------------------------------------------------------- helpers

    private async Task<string> RegisterVerifiedLandlordAsync(string email)
    {
        var (_, userId) = await RegisterAsync(email, "Landlord");

        // Real verification path: the Admin approval is represented by flipping Users.IsVerified, then
        // logging in again so the JWT carries is_verified=true (VerifiedLandlordPolicy).
        var dbContext = _factory.Services.GetRequiredService<SqliteDbContext>();
        var connection = dbContext.CreateOpenConnection();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE Users SET IsVerified = 1 WHERE Id = @id;";
            command.Parameters.AddWithValue("@id", userId);
            await command.ExecuteNonQueryAsync();
        }

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }

    private async Task<(string Token, string UserId)> RegisterAsync(string email, string role)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterUserRequest(email, Password, "Nguyen Van Kiem Thu", role, PhoneNumber: "0912345678"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (
            body.RootElement.GetProperty("accessToken").GetString()!,
            body.RootElement.GetProperty("user").GetProperty("id").GetString()!);
    }

    private async Task<string> GetUserIdAsync(string email)
    {
        var dbContext = _factory.Services.GetRequiredService<SqliteDbContext>();
        var connection = dbContext.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Users WHERE NormalizedEmail = @email;";
        command.Parameters.AddWithValue("@email", email.ToUpperInvariant());
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private async Task<string> UploadPhotoAsync(string token, byte[] bytes)
    {
        var response = await UploadPhotoResponseAsync(token, bytes, "photo.jpg");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<HttpResponseMessage> UploadPhotoResponseAsync(
        string token,
        byte[] bytes,
        string fileName,
        string? kind = "Standard")
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(file, "file", fileName);
        if (kind is not null)
        {
            content.Add(new StringContent(kind), "kind");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/landlord/photos") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        string token,
        object? body = null,
        string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await _client.SendAsync(request);
    }

    private static object ListingPayload(IReadOnlyList<string> photoIds) => new
    {
        title = "Phòng trọ sinh viên gần ICTU",
        description = "Phòng sạch, gần trường.",
        roomType = "BoardingRoom",
        monthlyRentVnd = 1_800_000,
        depositVnd = 1_800_000,
        areaM2 = 20.0,
        maxOccupants = 2,
        furnishing = "Basic",
        availableFrom = "2026-12-01",
        address = new { addressLine = "Tân Thịnh", ward = "Tân Thịnh", district = "TP. Thái Nguyên" },
        location = new { latitude = 21.5851, longitude = 105.8066 },
        utilities = new
        {
            electricityPriceVndPerKwh = 3_800,
            waterPriceVnd = 70_000,
            waterPriceUnit = "PerPerson",
            internetFeeVnd = 120_000,
            parkingFeeVnd = 60_000,
            otherFeesNote = (string?)null
        },
        amenities = new[] { "Wifi", "Bed" },
        safetyFeatures = new[] { "Cctv" },
        photoIds,
        houseRules = new
        {
            quietHoursStart = "22:00",
            quietHoursEnd = "06:00",
            curfewTime = "23:30",
            guestPolicy = "DaytimeOnly",
            guestPolicyNote = "Khách báo trước.",
            cookingPolicy = "SharedKitchenOnly",
            parkingAvailable = true,
            parkingRules = "Xe máy xếp hàng.",
            petsAllowed = false,
            smokingAllowed = false,
            otherRules = "Không tổ chức tiệc."
        }
    };

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(expectedStatus), payload);
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });

        using var problem = JsonDocument.Parse(payload);
        Assert.That(problem.RootElement.GetProperty("errorCode").GetString(), Is.EqualTo(expectedCode), payload);
    }
}
