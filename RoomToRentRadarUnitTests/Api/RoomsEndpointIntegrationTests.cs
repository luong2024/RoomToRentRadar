using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// Test tích hợp HTTP thật (US04 - T04.1): boot cả pipeline API (middleware, controller,
/// DI, SQLite in-memory có seed) qua <see cref="WebApplicationFactory{TEntryPoint}"/>.
/// Mỗi test tự tạo và dispose factory để cô lập dữ liệu in-memory.
/// </summary>
[TestFixture]
public class RoomsEndpointIntegrationTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseProvider"] = "Sqlite",
                    ["Sqlite:ConnectionString"] = "Data Source=:memory:",
                    ["Jwt:SigningKey"] = JwtSigningKey,
                    ["Jwt:Issuer"] = "RoomToRentRadar.Api",
                    ["Jwt:Audience"] = "RoomToRentRadar.Client"
                });
            });
        });

        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetRooms_WithoutQuery_ReturnsVerifiedAndAvailableSeedWithDistance()
    {
        var response = await _client.GetAsync("/api/rooms");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("totalCount").GetInt32(), Is.EqualTo(6),
                "Seed 8 phòng, 6 phòng đã xác minh và còn trống");
            Assert.That(root.GetProperty("pageNumber").GetInt32(), Is.EqualTo(1));
            Assert.That(root.GetProperty("pageSize").GetInt32(), Is.EqualTo(10));
            Assert.That(root.GetProperty("items").GetArrayLength(), Is.EqualTo(6));

            foreach (var item in root.GetProperty("items").EnumerateArray())
            {
                Assert.That(item.GetProperty("id").GetString(), Is.Not.Empty);
                Assert.That(item.GetProperty("isVerified").GetBoolean(), Is.True);
                Assert.That(item.GetProperty("isAvailable").GetBoolean(), Is.True);
                Assert.That(item.TryGetProperty("distanceKm", out var distance), Is.True, "Mỗi item phải có distanceKm");
                Assert.That(distance.ValueKind, Is.EqualTo(JsonValueKind.Number));
                Assert.That(distance.GetDouble(), Is.GreaterThanOrEqualTo(0d));
            }
        });
    }

    [Test]
    public async Task GetRooms_MinPriceGreaterThanMaxPrice_Returns400ProblemDetails()
    {
        var response = await _client.GetAsync("/api/rooms?minPrice=3000000&maxPrice=1000000");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Multiple(() =>
        {
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
            Assert.That(body.RootElement.GetProperty("title").GetString(), Is.EqualTo("Validation Error"));
            Assert.That(body.RootElement.GetProperty("detail").GetString(),
                Is.EqualTo("MinPrice cannot be greater than MaxPrice or negative."));
        });
    }

    [Test]
    public async Task GetRooms_PageSizeAboveMaximum_IsClampedTo50()
    {
        var response = await _client.GetAsync("/api/rooms?pageSize=1000");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var paged = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(paged.GetProperty("pageSize").GetInt32(), Is.EqualTo(50));
    }
}
