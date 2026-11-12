using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Api;

[TestFixture]
public class RoomDetailEndpointIntegrationTests
{
    private const string TestSigningKey = "test-signing-key-32-bytes-long-super-secret-12345";
    private const string Issuer = "RoomToRentRadar.Api";
    private const string Audience = "RoomToRentRadar.Client";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Jwt:SigningKey", TestSigningKey);
            builder.UseSetting("Jwt:Issuer", Issuer);
            builder.UseSetting("Jwt:Audience", Audience);
            builder.UseSetting("DatabaseProvider", "Sqlite");
            builder.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
        });

        _client = _factory.CreateClient();

        // Seed specific fixtures into the active datastore for deterministic integration tests
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRoomRepository>();

        var testRoom = new Room(
            id: "room-ictu-001",
            title: "Phòng trọ cao cấp gần cổng chính ICTU",
            description: "Phòng đầy đủ tiện nghi, an ninh tốt",
            monthlyRent: 1800000m,
            depositAmount: 1000000m,
            areaSquareMeters: 22.0,
            roomType: "BoardingRoom",
            amenities: new[] { "Wifi", "Điều hòa", "Nóng lạnh" },
            fullAddress: "Số 12 Đường Z115, Tân Thịnh, TP. Thái Nguyên",
            landlordName: "Cô Lan",
            landlordPhoneNumber: "0988123456",
            photoUrls: new[] { "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267" },
            panorama360Url: "https://my.matterport.com/show/?m=sample360room1",
            houseRules: new[] { "Không nuôi thú cưng", "Khóa cổng sau 23h30" },
            status: ListingStatus.Available,
            lastUpdatedAt: new DateTime(2026, 11, 10, 8, 30, 0, DateTimeKind.Utc),
            isVerified: true,
            distanceToCampusKm: 0.2,
            latitude: 21.5875,
            longitude: 105.8080
        );

        var unpublishedRoom = new Room(
            id: "room-ictu-003",
            title: "Phòng trọ giá rẻ sinh viên Quyết Thắng",
            description: "Phòng cơ bản, giá cả hợp lý",
            monthlyRent: 1200000m,
            depositAmount: 500000m,
            areaSquareMeters: 16.0,
            roomType: "BoardingRoom",
            amenities: new[] { "Wifi" },
            fullAddress: "Tổ 8 Phường Quyết Thắng, TP. Thái Nguyên",
            landlordName: "Bác Hùng",
            landlordPhoneNumber: "0977888999",
            photoUrls: new[] { "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2" },
            houseRules: new[] { "Không hút thuốc trong phòng" },
            status: ListingStatus.Unpublished,
            lastUpdatedAt: new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc),
            isVerified: true,
            distanceToCampusKm: 1.2,
            latitude: 21.5900,
            longitude: 105.8020
        );

        var rentedRoom = new Room(
            id: "room-ictu-004",
            title: "Phòng khép kín sạch đẹp gần cổng trường",
            description: "Phòng mới sửa, có gác xép",
            monthlyRent: 1600000m,
            depositAmount: 800000m,
            areaSquareMeters: 20.0,
            roomType: "BoardingRoom",
            amenities: new[] { "Wifi", "Bình nóng lạnh" },
            fullAddress: "Cổng sau ĐH CNTT & TT, Tân Thịnh, TP. Thái Nguyên",
            landlordName: "Anh Nam",
            landlordPhoneNumber: "0966554433",
            photoUrls: new[] { "https://images.unsplash.com/photo-1505691938895-1758d7feb511" },
            houseRules: new[] { "Đóng tiền nhà đúng ngày mùng 5" },
            status: ListingStatus.Rented,
            lastUpdatedAt: new DateTime(2026, 11, 8, 9, 0, 0, DateTimeKind.Utc),
            isVerified: false,
            distanceToCampusKm: 0.8,
            latitude: 21.5840,
            longitude: 105.8055
        );

        var pendingRoom = new Room(
            id: "room-pending-01",
            title: "Phòng chờ duyệt chưa công khai",
            description: "Chưa duyệt",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            roomType: "BoardingRoom",
            status: ListingStatus.PendingReview,
            latitude: 21.5860,
            longitude: 105.8070
        );

        await repo.AddAsync(testRoom);
        await repo.AddAsync(unpublishedRoom);
        await repo.AddAsync(rentedRoom);
        await repo.AddAsync(pendingRoom);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static string GenerateJwt(string userId = "student-001", string role = "Student")
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(TestSigningKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim("sub", userId),
                new Claim("role", role)
            }),
            Expires = DateTime.UtcNow.AddHours(2),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    [Test]
    public async Task GetRoomById_AnonymousUser_Returns200WithMaskedPhoneNumber()
    {
        // Act (AC4: Guest/unauthenticated user receives null phone number)
        var response = await _client.GetAsync("/api/rooms/room-ictu-001");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.That(root.GetProperty("id").GetString(), Is.EqualTo("room-ictu-001"));
        Assert.That(root.GetProperty("title").GetString(), Does.Contain("cổng chính ICTU"));
        Assert.That(root.GetProperty("monthlyRent").GetDecimal(), Is.EqualTo(1800000m));
        Assert.That(root.GetProperty("status").GetString(), Is.EqualTo("Available"));
        Assert.That(root.GetProperty("isAvailable").GetBoolean(), Is.True);

        // AC4 & BR-AUTH-07: Guest must receive null for landlord phone and zalo
        var landlord = root.GetProperty("landlord");
        Assert.That(landlord.GetProperty("phoneNumber").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(landlord.GetProperty("zaloUrl").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task GetRoomById_AuthenticatedUser_Returns200WithUnmaskedPhoneNumber()
    {
        // Arrange (AC4: Authenticated user receives landlord contact details)
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/rooms/room-ictu-001");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwt("student-001"));

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        var landlord = root.GetProperty("landlord");
        Assert.That(landlord.GetProperty("phoneNumber").GetString(), Is.EqualTo("0988123456"));
        Assert.That(landlord.GetProperty("zaloUrl").GetString(), Does.Contain("0988123456"));
    }

    [Test]
    public async Task GetRoomById_IncludesETagHeader_AndReturns304WhenNotModified()
    {
        // Act 1: Initial GET
        var initialResponse = await _client.GetAsync("/api/rooms/room-ictu-001");
        Assert.That(initialResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var etag = initialResponse.Headers.ETag;
        Assert.That(etag, Is.Not.Null, "Response must include an ETag header per ADR-004");

        // Act 2: GET with If-None-Match
        using var conditionalRequest = new HttpRequestMessage(HttpMethod.Get, "/api/rooms/room-ictu-001");
        conditionalRequest.Headers.IfNoneMatch.Add(etag);

        var conditionalResponse = await _client.SendAsync(conditionalRequest);

        // Assert 2
        Assert.That(conditionalResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotModified),
            "Server must return 304 Not Modified when ETag matches per ADR-004");
    }

    [Test]
    public async Task GetRoomById_WithCampusIdParam_CalculatesDistanceDynamically()
    {
        // Arrange (BR-CMP-03): Default is ICTU (lat: 21.5851, lng: 105.8066)
        var ictuResponse = await _client.GetAsync("/api/rooms/room-ictu-001");
        var tnusResponse = await _client.GetAsync("/api/rooms/room-ictu-001?campusId=TNUS");

        Assert.That(ictuResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(tnusResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var ictuJson = await ictuResponse.Content.ReadFromJsonAsync<RoomDetailDto>(JsonOptions);
        var tnusJson = await tnusResponse.Content.ReadFromJsonAsync<RoomDetailDto>(JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(ictuJson, Is.Not.Null);
            Assert.That(tnusJson, Is.Not.Null);
            Assert.That(ictuJson!.DistanceKm, Is.GreaterThan(0));
            Assert.That(tnusJson!.DistanceKm, Is.GreaterThan(0));
            Assert.That(ictuJson.DistanceKm, Is.Not.EqualTo(tnusJson.DistanceKm),
                "Distance to TNUS must differ from default ICTU distance per BR-CMP-03");
        });
    }

    [TestCase(" ")]
    [TestCase("   ")]
    public async Task GetRoomById_WhitespaceId_Returns400BadRequestWithProblemDetails(string invalidId)
    {
        // Act
        var response = await _client.GetAsync($"/api/rooms/{Uri.EscapeDataString(invalidId)}");

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        var hasErrorCode = root.TryGetProperty("errorCode", out var code) ||
                           (root.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("errorCode", out code));

        Assert.That(hasErrorCode, Is.True, "ProblemDetails must contain errorCode");
        Assert.That(code.GetString(), Is.EqualTo("VALIDATION_FAILED"));
    }

    [Test]
    public async Task GetRoomById_NonExistentRoom_Returns404NotFoundWithProblemDetails()
    {
        // Act
        var response = await _client.GetAsync("/api/rooms/room-non-existent-xyz");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        var hasErrorCode = root.TryGetProperty("errorCode", out var code) ||
                           (root.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("errorCode", out code));

        Assert.That(hasErrorCode, Is.True, "ProblemDetails must contain errorCode");
        Assert.That(code.GetString(), Is.EqualTo("ROOM_NOT_FOUND"));
    }

    [Test]
    public async Task GetRoomById_PendingVerificationRoom_Returns404NotFound()
    {
        // Act (BR-LST-04a: Draft / Pending review rooms are hidden from public)
        var response = await _client.GetAsync("/api/rooms/room-pending-01");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        var hasErrorCode = root.TryGetProperty("errorCode", out var code) ||
                           (root.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("errorCode", out code));

        Assert.That(hasErrorCode, Is.True, "ProblemDetails must contain errorCode");
        Assert.That(code.GetString(), Is.EqualTo("ROOM_NOT_FOUND"));
    }

    [Test]
    public async Task GetRoomById_UnpublishedAndRentedRooms_AreViewableOnDetailPage()
    {
        // Act (BR-LST-04a: Available, Rented, and Unpublished are viewable on detail page)
        var unpubResponse = await _client.GetAsync("/api/rooms/room-ictu-003");
        var rentedResponse = await _client.GetAsync("/api/rooms/room-ictu-004");

        // Assert
        Assert.That(unpubResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(rentedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var unpubJson = await unpubResponse.Content.ReadFromJsonAsync<RoomDetailDto>(JsonOptions);
        var rentedJson = await rentedResponse.Content.ReadFromJsonAsync<RoomDetailDto>(JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(unpubJson!.Status, Is.EqualTo(ListingStatus.Unpublished));
            Assert.That(unpubJson.IsAvailable, Is.False);

            Assert.That(rentedJson!.Status, Is.EqualTo(ListingStatus.Rented));
            Assert.That(rentedJson.IsAvailable, Is.False);
        });
    }

    [Test]
    public async Task GetSimilarRooms_DedicatedEndpoint_Returns200WithSimilarRooms()
    {
        // Act (getSimilarRooms, BR-LST-10, US02 AC3)
        var response = await _client.GetAsync("/api/rooms/room-ictu-001/similar");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var similar = await response.Content.ReadFromJsonAsync<List<RoomCardDto>>(JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(similar, Is.Not.Null);
            Assert.That(similar!.Count, Is.LessThanOrEqualTo(5));

            foreach (var card in similar)
            {
                Assert.That(card.Id, Is.Not.EqualTo("room-ictu-001"), "Must exclude target room itself");
                Assert.That(card.IsAvailable, Is.True, "Must only return Available rooms");
                Assert.That(card.DistanceKm, Is.LessThanOrEqualTo(2.0), "Must be within 2 km radius (BR-LST-10)");
            }
        });
    }

    [Test]
    public async Task GetSimilarRooms_NonExistentRoom_Returns404NotFoundWithProblemDetails()
    {
        // Act
        var response = await _client.GetAsync("/api/rooms/room-non-existing/similar");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        var hasErrorCode = root.TryGetProperty("errorCode", out var code) ||
                           (root.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("errorCode", out code));

        Assert.That(hasErrorCode, Is.True, "ProblemDetails must contain errorCode");
        Assert.That(code.GetString(), Is.EqualTo("ROOM_NOT_FOUND"));
    }
}
