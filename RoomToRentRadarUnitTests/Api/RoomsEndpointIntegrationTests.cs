using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// US04 / T04.1 — <c>operationId: searchRooms</c> qua pipeline HTTP thật
/// (<see cref="WebApplicationFactory{TEntryPoint}"/>), kiểm hợp đồng OpenAPI và mã lỗi ADR-012.
/// Mỗi test tạo factory riêng để cô lập SQLite in-memory.
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
        _factory = CreateFactory(seedDemoRooms: true);
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>
    /// Ruling #24: <c>Program</c> gọi <c>WebApplication.CreateBuilder(args)</c> nên
    /// <c>ConfigureAppConfiguration</c> của <see cref="WebHostBuilderExtensions"/> không tới được
    /// <c>builder.Configuration</c>. Dùng <c>UseSetting</c> (chuyển thành host configuration, được đưa
    /// vào <c>builder.Configuration</c>) để test luôn chạy SQLite <c>:memory:</c> và cờ seed đúng.
    /// </summary>
    private static WebApplicationFactory<Program> CreateFactory(bool seedDemoRooms) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Sqlite:SeedDemoRooms", seedDemoRooms ? "true" : "false");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
        });

    [Test]
    public async Task GetRooms_WithoutQuery_ReturnsFiveAvailableSeededCards()
    {
        var response = await _client.GetAsync("/api/rooms");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("totalCount").GetInt32(), Is.EqualTo(5), "Seed demo có 5 phòng Available");
            Assert.That(root.GetProperty("pageNumber").GetInt32(), Is.EqualTo(1));
            Assert.That(root.GetProperty("pageSize").GetInt32(), Is.EqualTo(20));
            Assert.That(root.GetProperty("items").GetArrayLength(), Is.EqualTo(5));

            foreach (var item in root.GetProperty("items").EnumerateArray())
            {
                Assert.That(item.GetProperty("status").GetString(), Is.EqualTo("Available"), "enum JSON dạng chuỗi");
                Assert.That(item.GetProperty("roomType").ValueKind, Is.EqualTo(JsonValueKind.String));
                Assert.That(item.GetProperty("isVerified").GetBoolean(), Is.True);
                Assert.That(item.GetProperty("distanceKm").GetDouble(), Is.GreaterThanOrEqualTo(0d));
                Assert.That(item.TryGetProperty("isAvailable", out _), Is.False, "schema mới không còn isAvailable");
            }
        });
    }

    [Test]
    public async Task GetRooms_RoomTypesCommaSeparated_FiltersByType()
    {
        var response = await _client.GetAsync("/api/rooms?roomTypes=MiniApartment,WholeHouse");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();

        Assert.That(items, Is.Not.Empty);
        Assert.That(
            items.Select(item => item.GetProperty("roomType").GetString()).ToList(),
            Has.All.Matches<string?>(roomType => roomType is "MiniApartment" or "WholeHouse"));
    }

    [TestCase("/api/rooms?sort=area_asc")]
    [TestCase("/api/rooms?minRentVnd=3000000&maxRentVnd=1000000")]
    [TestCase("/api/rooms?campusId=NOPE")]
    [TestCase("/api/rooms?maxDistanceKm=NaN")]
    [TestCase("/api/rooms?maxDistanceKm=Infinity")]
    [TestCase("/api/rooms?maxDistanceKm=-Infinity")]
    [TestCase("/api/rooms?maxAreaM2=NaN")]
    public async Task GetRooms_InvalidInput_Returns400ProblemDetailsWithErrorCode(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), url);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Multiple(() =>
        {
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
            Assert.That(body.RootElement.GetProperty("errorCode").GetString(), Is.Not.Empty, "ADR-012 errorCode");
        });
    }

    [TestCase("/api/rooms?pageNumber=abc")]
    [TestCase("/api/rooms?minRentVnd=1.5")]
    [TestCase("/api/rooms?roomTypes=Bogus")]
    public async Task GetRooms_ModelBindingFailure_ReturnsValidationFailedProblemDetails(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), url);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Multiple(() =>
        {
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
            Assert.That(body.RootElement.GetProperty("errorCode").GetString(), Is.EqualTo("VALIDATION_FAILED"),
                "system-architecture §7.2: lỗi model-binding dùng cùng errorCode với domain validation");
            Assert.That(body.RootElement.TryGetProperty("errors", out var errors), Is.True, "ADR-012: phải có errors{{}}");
            Assert.That(errors.ValueKind, Is.EqualTo(JsonValueKind.Object));
        });
    }

    [Test]
    public async Task GetRooms_CombinedFilters_AreAppliedEndToEnd()
    {
        // US04 AC2: price + area + distance + room type must compose down to SQL, then exact Haversine.
        var response = await _client.GetAsync(
            "/api/rooms?minRentVnd=1000000&maxRentVnd=2500000&minAreaM2=15&maxAreaM2=25" +
            "&maxDistanceKm=3&roomTypes=BoardingRoom");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = body.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .ToList();

        Assert.That(ids, Is.EquivalentTo(new[] { "seed-z115", "seed-thao-nguyen" }));
    }

    [Test]
    public async Task GetRooms_NumericRoomType_IsRejected()
    {
        // GUARDRAILS §1.5.1: whitelisting accepts enum NAMES only — "1" used to bind to BoardingRoom.
        var response = await _client.GetAsync("/api/rooms?roomTypes=1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetRooms_EmptySortValue_IsRejectedInsteadOfDefaulting()
    {
        // Ruling 21: absent `sort` = default; present-but-empty `sort=` = invalid input.
        var response = await _client.GetAsync("/api/rooms?sort=");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("errorCode").GetString(), Is.EqualTo("INVALID_SORT_OPTION"));
    }

    [Test]
    public async Task GetRooms_UnknownSort_ReturnsInvalidSortOptionErrorCode()
    {
        var response = await _client.GetAsync("/api/rooms?sort=price");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(body.RootElement.GetProperty("errorCode").GetString(), Is.EqualTo("INVALID_SORT_OPTION"));
    }

    [Test]
    public async Task GetRooms_UnknownCampus_ReturnsCampusNotFoundErrorCode()
    {
        var response = await _client.GetAsync("/api/rooms?campusId=NOPE");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(body.RootElement.GetProperty("errorCode").GetString(), Is.EqualTo("CAMPUS_NOT_FOUND"));
    }

    [Test]
    public async Task GetRooms_PageSizeAboveMaximum_IsClampedTo50()
    {
        var response = await _client.GetAsync("/api/rooms?pageSize=1000");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("pageSize").GetInt32(), Is.EqualTo(50));
    }

    [Test]
    public async Task GetRooms_InvalidRoomType_Returns400ProblemDetails()
    {
        var response = await _client.GetAsync("/api/rooms?roomTypes=BOGUS");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(
            response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/problem+json"),
            "ADR-012: lỗi trả problem+json");

        // Follow-up: model-state 400 chưa có `errorCode` (cần InvalidModelStateResponseFactory),
        // nên test này CỐ Ý không assert `errorCode`. Bật lại khi follow-up hoàn tất.
    }

    [Test]
    public async Task GetRooms_EmptyRoomTypeFilter_IsIgnored()
    {
        // `?roomTypes=` bind thành null (= không lọc theo loại phòng), không phải list rỗng (list rỗng = 0 phòng).
        var response = await _client.GetAsync("/api/rooms?roomTypes=");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("totalCount").GetInt32(), Is.EqualTo(5), "roomTypes rỗng = không lọc → toàn bộ seed demo");
            Assert.That(root.GetProperty("items").GetArrayLength(), Is.EqualTo(5));
        });
    }

    [Test]
    public async Task GetRooms_DemoSeedDisabled_ReturnsEmptyPage()
    {
        using var productionLike = CreateFactory(seedDemoRooms: false);
        using var client = productionLike.CreateClient();

        var response = await client.GetAsync("/api/rooms");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Multiple(() =>
        {
            Assert.That(body.RootElement.GetProperty("totalCount").GetInt32(), Is.Zero, "H4: không seed demo ngoài Development/flag");
            Assert.That(body.RootElement.GetProperty("items").GetArrayLength(), Is.Zero);
        });
    }
}
