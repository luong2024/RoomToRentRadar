using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Datastore.Sqlite.Context;

namespace RoomToRentRadarUnitTests.Api;

[TestFixture]
public class DemoSeedEndpointIntegrationTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";

    [Test]
    public async Task GetRooms_ByCampus_ReturnsDifferentNearestRooms()
    {
        using var factory = CreateFactory(environment: "Development", seedDemoRooms: true, seedExtended: true);
        using var client = factory.CreateClient();

        var allResponse = await client.GetAsync("/api/rooms?campusId=ICTU&pageSize=50");
        var ictuNearResponse = await client.GetAsync("/api/rooms?campusId=ICTU&maxDistanceKm=2&pageSize=50");
        var tnutNearResponse = await client.GetAsync("/api/rooms?campusId=TNUT&maxDistanceKm=2&pageSize=50");

        Assert.Multiple(() =>
        {
            Assert.That(allResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(ictuNearResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(tnutNearResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });

        using var allDoc = JsonDocument.Parse(await allResponse.Content.ReadAsStringAsync());
        using var ictuDoc = JsonDocument.Parse(await ictuNearResponse.Content.ReadAsStringAsync());
        using var tnutDoc = JsonDocument.Parse(await tnutNearResponse.Content.ReadAsStringAsync());

        var ictuIds = ictuDoc.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!)
            .ToList();
        var tnutItems = tnutDoc.RootElement.GetProperty("items").EnumerateArray().ToList();
        var tnutIds = tnutItems
            .Select(item => item.GetProperty("id").GetString()!)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(allDoc.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(22));
            Assert.That(ictuIds, Has.Count.GreaterThanOrEqualTo(4));
            Assert.That(tnutIds, Has.Count.GreaterThanOrEqualTo(4));
            Assert.That(tnutIds, Is.Not.EquivalentTo(ictuIds));
            Assert.That(tnutItems.All(i => i.GetProperty("status").GetString() == "Available"), Is.True);
        });
    }

    [Test]
    public async Task Production_NoDemoRoomsNoDemoAccounts()
    {
        using var factory = CreateFactory(environment: "Production");
        using var client = factory.CreateClient();

        var roomsResponse = await client.GetAsync("/api/rooms");
        Assert.That(roomsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var roomsDoc = JsonDocument.Parse(await roomsResponse.Content.ReadAsStringAsync());
        Assert.That(roomsDoc.RootElement.GetProperty("totalCount").GetInt32(), Is.Zero);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SqliteDbContext>();
        using var conn = dbContext.CreateOpenConnection();

        using var roomsCmd = conn.CreateCommand();
        roomsCmd.CommandText = "SELECT COUNT(1) FROM Rooms;";
        var roomCount = Convert.ToInt64(roomsCmd.ExecuteScalar());

        using var usersCmd = conn.CreateCommand();
        usersCmd.CommandText = "SELECT COUNT(1) FROM Users;";
        var userCount = Convert.ToInt64(usersCmd.ExecuteScalar());

        Assert.Multiple(() =>
        {
            Assert.That(roomCount, Is.Zero);
            Assert.That(userCount, Is.Zero);
        });
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string environment,
        bool? seedDemoRooms = null,
        bool? seedExtended = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment(environment);
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            if (seedDemoRooms.HasValue)
            {
                webHost.UseSetting("Sqlite:SeedDemoRooms", seedDemoRooms.Value ? "true" : "false");
            }

            if (seedExtended.HasValue)
            {
                webHost.UseSetting("Sqlite:SeedExtendedDemoData", seedExtended.Value ? "true" : "false");
            }

            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
        });
}
