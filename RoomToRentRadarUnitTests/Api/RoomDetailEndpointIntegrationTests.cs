namespace RoomToRentRadarUnitTests.Api;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// US02 / T02.1 — Room Detail & Similar Rooms API Integration Tests
/// Verifies AC1..AC4, Weak ETag caching (ADR-012 §6), and error contracts.
/// </summary>
[TestFixture]
public class RoomDetailEndpointIntegrationTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Sqlite:SeedDemoRooms", "true");
            webHost.UseSetting("Sqlite:SeedExtendedDemoData", "false");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
        });
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static string GenerateJwt(string userId, string role = "Student")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "RoomToRentRadar.Api",
            audience: "RoomToRentRadar.Client",
            claims: new[]
            {
                new Claim("sub", userId),
                new Claim("role", role)
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Test]
    public async Task GetRoomById_Anonymous_ReturnsRoomDetailWithMaskedPhoneAndCacheHeaders()
    {
        // Act (AC1, AC4, ADR-012 §6)
        var response = await _client.GetAsync("/api/rooms/seed-z115");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Cache headers
        Assert.That(response.Headers.CacheControl?.Private, Is.True);
        Assert.That(response.Headers.CacheControl?.NoCache, Is.True);
        Assert.That(response.Headers.Vary, Does.Contain("Authorization"));
        Assert.That(response.Headers.ETag, Is.Not.Null);
        Assert.That(response.Headers.ETag!.IsWeak, Is.True);

        var detail = await response.Content.ReadFromJsonAsync<RoomDetailDto>(JsonOptions);
        Assert.That(detail, Is.Not.Null);
        Assert.That(detail!.Id, Is.EqualTo("seed-z115"));
        Assert.That(detail.Title, Is.EqualTo("Phòng trọ Sinh viên Z115"));
        Assert.That(detail.MonthlyRentVnd, Is.EqualTo(1_500_000L));
        Assert.That(detail.Status, Is.EqualTo(ListingStatus.Available));
        Assert.That(detail.IsVerified, Is.True);
        Assert.That(detail.Landlord.PhoneNumber, Is.Null, "AC4: Anonymous caller must not see landlord phone number");
        Assert.That(detail.Landlord.ZaloUrl, Is.Null, "AC4: Anonymous caller must not see landlord Zalo URL");
    }

    [Test]
    public async Task GetRoomById_AuthenticatedUser_ReturnsUnmaskedLandlordPhoneNumber()
    {
        // Arrange (AC4)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomToRentRadar.Datastore.Sqlite.Context.SqliteDbContext>();
            var conn = db.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Users SET PhoneNumber = '0988123456' WHERE Id = 'seed-landlord';";
            cmd.ExecuteNonQuery();
        }

        var token = GenerateJwt("student-123");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/rooms/seed-z115");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var detail = await response.Content.ReadFromJsonAsync<RoomDetailDto>(JsonOptions);
        Assert.That(detail, Is.Not.Null);
        Assert.That(detail!.Landlord.PhoneNumber, Is.Not.Null.And.Not.Empty, "AC4: Signed-in user must see landlord phone");
    }

    [Test]
    public async Task GetRoomById_WithMatchingIfNoneMatch_Returns304NotModified()
    {
        // Arrange: Fetch to obtain ETag
        var firstResponse = await _client.GetAsync("/api/rooms/seed-z115");
        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var etag = firstResponse.Headers.ETag!.ToString();

        // Act: Send request with If-None-Match
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/rooms/seed-z115");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var secondResponse = await _client.SendAsync(request);

        // Assert (ADR-012 §6)
        Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));
    }

    [Test]
    public async Task GetRoomById_WhenNonExistent_Returns404WithRoomNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/rooms/non-existent-room-id");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("errorCode").GetString(), Is.EqualTo("ROOM_NOT_FOUND"));
    }

    [Test]
    public async Task GetRoomById_WhenCampusUnknown_Returns400WithCampusNotFound()
    {
        // Act (BR-CMP-01)
        var response = await _client.GetAsync("/api/rooms/seed-z115?campusId=UNKNOWN_CAMPUS");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("errorCode").GetString(), Is.EqualTo("CAMPUS_NOT_FOUND"));
    }

    [Test]
    public async Task GetSimilarRooms_Returns200WithNearbyRooms()
    {
        // Act (AC3 / BR-LST-10)
        var response = await _client.GetAsync("/api/rooms/seed-z115/similar");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var list = await response.Content.ReadFromJsonAsync<List<RoomCardDto>>(JsonOptions);
        Assert.That(list, Is.Not.Null);
        Assert.That(list!.Count, Is.LessThanOrEqualTo(5));
        Assert.That(list.All(r => r.Id != "seed-z115"), Is.True, "Must exclude target room itself");
    }

    [Test]
    public async Task GetSimilarRooms_WhenRoomNotFound_Returns404WithRoomNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/rooms/non-existent-id/similar");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("errorCode").GetString(), Is.EqualTo("ROOM_NOT_FOUND"));
    }

    [Test]
    public async Task GetRoomById_WhenRoomIsPendingReview_EnforcesOwnershipAccess()
    {
        // Arrange (BR-LST-04a / anti-IDOR)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomToRentRadar.Datastore.Sqlite.Context.SqliteDbContext>();
            var conn = db.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Rooms SET Status = 'PendingReview' WHERE Id = 'seed-z115';";
            cmd.ExecuteNonQuery();
        }

        // Act 1: Anonymous caller receives 404
        var anonResponse = await _client.GetAsync("/api/rooms/seed-z115");
        Assert.That(anonResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using (var body = JsonDocument.Parse(await anonResponse.Content.ReadAsStringAsync()))
        {
            Assert.That(body.RootElement.GetProperty("errorCode").GetString(), Is.EqualTo("ROOM_NOT_FOUND"));
        }

        // Act 2: Owner caller receives 200
        var ownerToken = GenerateJwt("seed-landlord", role: "Landlord");
        var ownerRequest = new HttpRequestMessage(HttpMethod.Get, "/api/rooms/seed-z115");
        ownerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var ownerResponse = await _client.SendAsync(ownerRequest);

        // Assert
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Cleanup
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomToRentRadar.Datastore.Sqlite.Context.SqliteDbContext>();
            var conn = db.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Rooms SET Status = 'Available' WHERE Id = 'seed-z115';";
            cmd.ExecuteNonQuery();
        }
    }

    [Test]
    public async Task GetSimilarRooms_WhenTargetRoomIsPendingReview_EnforcesOwnershipAccess()
    {
        // Arrange (BR-LST-04a / anti-IDOR)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomToRentRadar.Datastore.Sqlite.Context.SqliteDbContext>();
            var conn = db.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Rooms SET Status = 'PendingReview' WHERE Id = 'seed-z115';";
            cmd.ExecuteNonQuery();
        }

        // Act 1: Anonymous caller receives 404
        var anonResponse = await _client.GetAsync("/api/rooms/seed-z115/similar");
        Assert.That(anonResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using (var body = JsonDocument.Parse(await anonResponse.Content.ReadAsStringAsync()))
        {
            Assert.That(body.RootElement.GetProperty("errorCode").GetString(), Is.EqualTo("ROOM_NOT_FOUND"));
        }

        // Act 2: Owner caller receives 200
        var ownerToken = GenerateJwt("seed-landlord", role: "Landlord");
        var ownerRequest = new HttpRequestMessage(HttpMethod.Get, "/api/rooms/seed-z115/similar");
        ownerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var ownerResponse = await _client.SendAsync(ownerRequest);

        // Assert
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Cleanup
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomToRentRadar.Datastore.Sqlite.Context.SqliteDbContext>();
            var conn = db.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Rooms SET Status = 'Available' WHERE Id = 'seed-z115';";
            cmd.ExecuteNonQuery();
        }
    }

    [Test]
    public async Task GetRoomById_WithExtendedDemoSeed_ReturnsAmenitiesSafetyHouseRulesAndOtherFeesNote()
    {
        using var extendedFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Sqlite:SeedDemoRooms", "true");
            webHost.UseSetting("Sqlite:SeedExtendedDemoData", "true");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
        });
        using var extendedClient = extendedFactory.CreateClient();

        var demo02Response = await extendedClient.GetAsync("/api/rooms/demo-02");
        Assert.That(demo02Response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var demo02 = await demo02Response.Content.ReadFromJsonAsync<RoomDetailDto>(JsonOptions);

        var seedResponse = await extendedClient.GetAsync("/api/rooms/seed-z115");
        Assert.That(seedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var seed = await seedResponse.Content.ReadFromJsonAsync<RoomDetailDto>(JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(demo02, Is.Not.Null);
            Assert.That(demo02!.Amenities, Is.Not.Empty);
            Assert.That(demo02.Safety.IsVerified, Is.True);
            Assert.That(demo02.Safety.Score, Is.EqualTo(3));
            Assert.That(demo02.Safety.Features, Has.Count.EqualTo(3));
            Assert.That(demo02.HouseRules, Is.Not.Null);
            Assert.That(demo02.HouseRules!.VersionNumber, Is.EqualTo(2));
            Assert.That(demo02.HouseRules.CurfewTime, Is.EqualTo("23:30"));
            Assert.That(demo02.Utilities.OtherFeesNote, Is.EqualTo("Vệ sinh chung 20.000 đ/tháng"));

            Assert.That(seed, Is.Not.Null);
            Assert.That(seed!.Utilities.OtherFeesNote, Is.EqualTo("Rác 20.000 đ/tháng"));
            Assert.That(seed.Amenities, Is.Not.Empty);
            Assert.That(seed.HouseRules, Is.Not.Null);
            Assert.That(seed.Safety.Score, Is.EqualTo(3));
        });
    }
}
