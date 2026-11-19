using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// ADR-008 / system-architecture §6.1: uploaded files are served at <c>/media/{key}</c> with a
/// year-long immutable cache header; a missing key stays a problem+json 404 (ADR-012).
/// </summary>
[TestFixture]
public class MediaStaticFilesIntegrationTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";

    private string _mediaRoot = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), "rtrr-media-" + Guid.NewGuid().ToString("N"));
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
    public async Task GetMedia_ExistingFile_ReturnsItWithImmutableCacheHeader()
    {
        var key = "rooms/2026/11/photo-1/original.jpg";
        var path = Path.Combine(_mediaRoot, key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);

        var response = await _client.GetAsync("/media/" + key);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/jpeg"));
            Assert.That(
                response.Headers.CacheControl?.ToString(),
                Is.EqualTo("public, max-age=31536000, immutable"));
        });
    }

    [Test]
    public async Task GetMedia_MissingFile_ReturnsProblemJson404()
    {
        var response = await _client.GetAsync("/media/rooms/2026/11/nope/original.jpg");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
    }
}
