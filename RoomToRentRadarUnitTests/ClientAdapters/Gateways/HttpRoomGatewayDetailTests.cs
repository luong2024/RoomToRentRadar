using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadarUnitTests.ClientAdapters.Gateways;

/// <summary>
/// Phase 5 T02.2 Unit Tests for HttpRoomGateway: Room Detail & Similar Rooms endpoints.
/// Verifies OpenAPI endpoint pathing, query string encoding, and problem details mapping.
/// </summary>
[TestFixture]
public class HttpRoomGatewayDetailTests
{
    [Test]
    public async Task GetRoomDetailAsync_ValidRoomId_SendsGetRequestToCorrectEndpoint()
    {
        // Arrange
        var sampleDetail = CreateSampleDetail("room-xyz");
        var (gateway, stub) = CreateGateway(_ => JsonResponse(HttpStatusCode.OK, sampleDetail));

        // Act
        var result = await gateway.GetRoomDetailAsync("room-xyz");

        // Assert
        var recorded = stub.Requests.Single();
        Assert.That(recorded.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(recorded.Path, Is.EqualTo("/api/rooms/room-xyz"));
        Assert.That(string.IsNullOrEmpty(recorded.Uri.Query), Is.True);
        Assert.That(result.Id, Is.EqualTo("room-xyz"));
        Assert.That(result.Title, Is.EqualTo("Spacious Room near ICTU"));
        Assert.That(result.MonthlyRentVnd, Is.EqualTo(2_500_000));
        Assert.That(result.Utilities.EstimatedMonthlyTotalVnd, Is.EqualTo(2_950_000));
    }

    [Test]
    public async Task GetRoomDetailAsync_WithCampusId_AppendsEscapedCampusIdQuery()
    {
        // Arrange
        var sampleDetail = CreateSampleDetail("room-xyz");
        var (gateway, stub) = CreateGateway(_ => JsonResponse(HttpStatusCode.OK, sampleDetail));

        // Act
        await gateway.GetRoomDetailAsync("room-xyz", "ICTU / Thai Nguyen");

        // Assert
        var recorded = stub.Requests.Single();
        Assert.That(recorded.Path, Is.EqualTo("/api/rooms/room-xyz"));
        var query = HttpUtility.ParseQueryString(recorded.Uri.Query);
        Assert.That(query["campusId"], Is.EqualTo("ICTU / Thai Nguyen"));
    }

    [Test]
    public void GetRoomDetailAsync_RoomNotFound_ThrowsApiExceptionWithCorrectCode()
    {
        // Arrange
        var (gateway, _) = CreateGateway(_ => Problem(HttpStatusCode.NotFound, ProblemCodes.RoomNotFound));

        // Act & Assert
        var ex = Assert.ThrowsAsync<ApiException>(async () => await gateway.GetRoomDetailAsync("non-existent-id"));
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.RoomNotFound));
    }

    [Test]
    public void GetRoomDetailAsync_ServerError_ThrowsApiException()
    {
        // Arrange
        var (gateway, _) = CreateGateway(_ => Problem(HttpStatusCode.InternalServerError, ProblemCodes.InternalError));

        // Act & Assert
        var ex = Assert.ThrowsAsync<ApiException>(async () => await gateway.GetRoomDetailAsync("room-error"));
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
    }

    [Test]
    public async Task GetSimilarRoomsAsync_ValidRoomId_ReturnsListOfRoomCards()
    {
        // Arrange
        var cards = new List<RoomCardDto>
        {
            new("sim-1", "Similar Room 1", 2_400_000, 22.0, RoomType.BoardingRoom, 0.8, true, ListingStatus.Available, null, 21.5, 105.8, DateTime.UtcNow, false),
            new("sim-2", "Similar Room 2", 2_600_000, 24.0, RoomType.BoardingRoom, 1.1, false, ListingStatus.Available, null, 21.5, 105.8, DateTime.UtcNow, false)
        };
        var (gateway, stub) = CreateGateway(_ => JsonResponse(HttpStatusCode.OK, cards));

        // Act
        var result = await gateway.GetSimilarRoomsAsync("room-xyz");

        // Assert
        var recorded = stub.Requests.Single();
        Assert.That(recorded.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(recorded.Path, Is.EqualTo("/api/rooms/room-xyz/similar"));
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Id, Is.EqualTo("sim-1"));
        Assert.That(result[1].Id, Is.EqualTo("sim-2"));
    }

    [Test]
    public async Task GetSimilarRoomsAsync_WithCampusId_AppendsCampusIdQuery()
    {
        // Arrange
        var (gateway, stub) = CreateGateway(_ => JsonResponse(HttpStatusCode.OK, new List<RoomCardDto>()));

        // Act
        await gateway.GetSimilarRoomsAsync("room-xyz", "ictu");

        // Assert
        var recorded = stub.Requests.Single();
        Assert.That(recorded.Path, Is.EqualTo("/api/rooms/room-xyz/similar"));
        var query = HttpUtility.ParseQueryString(recorded.Uri.Query);
        Assert.That(query["campusId"], Is.EqualTo("ictu"));
    }

    [Test]
    public void GetSimilarRoomsAsync_RoomNotFound_ThrowsApiException()
    {
        // Arrange
        var (gateway, _) = CreateGateway(_ => Problem(HttpStatusCode.NotFound, ProblemCodes.RoomNotFound));

        // Act & Assert
        var ex = Assert.ThrowsAsync<ApiException>(async () => await gateway.GetSimilarRoomsAsync("non-existent-id"));
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.RoomNotFound));
    }

    private static (HttpRoomGateway Gateway, StubHttpMessageHandler Stub) CreateGateway(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var stub = new StubHttpMessageHandler(responder);
        var client = new HttpClient(stub) { BaseAddress = new Uri("http://localhost") };
        return (new HttpRoomGateway(client), stub);
    }

    private static RoomDetailDto CreateSampleDetail(string id) => new(
        Id: id,
        Title: "Spacious Room near ICTU",
        Description: "Full furnished, nice view.",
        RoomType: RoomType.BoardingRoom,
        Status: ListingStatus.Available,
        IsVerified: true,
        MonthlyRentVnd: 2_500_000,
        DepositVnd: 2_500_000,
        AreaM2: 25.0,
        MaxOccupants: 2,
        Furnishing: Furnishing.Full,
        AvailableFrom: new DateOnly(2026, 12, 1),
        Address: new AddressDto("Ngõ 12 Z115", "Quyết Thắng", "TP. Thái Nguyên"),
        Location: new GeoPointDto(21.5851, 105.8066),
        DistanceKm: 1.2,
        Utilities: new UtilitiesView(3500, 100000, WaterPriceUnit.PerPerson, 0, 50000, null, 2950000),
        Amenities: [Amenity.Wifi, Amenity.AirConditioner, Amenity.WaterHeater],
        Safety: new SafetyDto(4, true, [SafetyFeature.Cctv, SafetyFeature.SecureEntrance]),
        HouseRules: new HouseRulesDto("22:00", "06:00", "23:30", GuestPolicy.DaytimeOnly, null, CookingPolicy.SharedKitchenOnly, true, null, false, false, null, 1, DateTime.UtcNow),
        Photos: [],
        Landlord: new LandlordContactDto("landlord-1", "Nguyễn Thị Hoa", true, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), null, null),
        IsFavourite: false,
        PublishedAtUtc: DateTime.UtcNow.AddDays(-5),
        LastUpdatedAtUtc: DateTime.UtcNow.AddDays(-2)
    );

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) =>
        new(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, ApiResponseReader.JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

    private static HttpResponseMessage Problem(HttpStatusCode status, string errorCode) =>
        new(status)
        {
            Content = new StringContent(
                $$"""{"title":"Error","status":{{(int)status}},"errorCode":"{{errorCode}}"}""",
                Encoding.UTF8,
                "application/problem+json")
        };
}
