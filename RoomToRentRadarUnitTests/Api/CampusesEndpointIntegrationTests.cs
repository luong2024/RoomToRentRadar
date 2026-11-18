using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// T00.8 — GET /api/campuses integration test (BR-CMP-01).
/// Reuses the existing WebApplicationFactory pattern from RoomsEndpointIntegrationTests
/// with isolated SQLite in-memory database.
/// </summary>
[TestFixture]
public class CampusesEndpointIntegrationTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = CreateFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Sqlite:SeedDemoRooms", "false");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
        });

    [Test]
    public async Task GetCampuses_ReturnsOkWithEightCampusesAndIctuDefaultFirst()
    {
        var response = await _client.GetAsync("/api/campuses");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var campuses = await response.Content.ReadFromJsonAsync<List<CampusDto>>();

        Assert.That(campuses, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(campuses!.Count, Is.EqualTo(8), "8 campuses from Thái Nguyên University seeded (BR-CMP-01)");

            var firstCampus = campuses[0];
            Assert.That(firstCampus.Id, Is.EqualTo("ICTU"), "ICTU must be the first campus");
            Assert.That(firstCampus.IsDefault, Is.True, "ICTU isDefault must be true");

            // Remaining 7 campuses have isDefault = false
            for (var i = 1; i < campuses.Count; i++)
            {
                Assert.That(campuses[i].IsDefault, Is.False, $"Campus {campuses[i].Id} must not be default");
            }
        });
    }
}
