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
}
