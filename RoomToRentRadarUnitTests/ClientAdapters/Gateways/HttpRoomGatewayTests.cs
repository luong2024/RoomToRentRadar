using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadarUnitTests.ClientAdapters.Gateways;

/// <summary>
/// T00.14 Phase 6 (AC-2): HttpRoomGateway.SearchRoomsAsync builds the searchRooms query string
/// (10 openapi parameters, invariant numbers, enum names) and maps responses through ApiResponseReader.
/// </summary>
[TestFixture]
public class HttpRoomGatewayTests
{
    [Test]
    public async Task SearchRoomsAsync_DefaultRequest_CallsApiRoomsWithPaging()
    {
        var (gateway, stub) = CreateGateway(_ => OkPage(Card()));

        var result = await gateway.SearchRoomsAsync(new RoomSearchRequest());

        var recorded = stub.Requests.Single();
        Assert.That(recorded.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(recorded.Path, Is.EqualTo("/api/rooms"));
        var query = HttpUtility.ParseQueryString(recorded.Uri.Query);
        Assert.That(query.AllKeys, Is.EquivalentTo(new[] { "pageNumber", "pageSize" }));
        Assert.That(query["pageNumber"], Is.EqualTo("1"));
        Assert.That(query["pageSize"], Is.EqualTo("20"));
        Assert.That(result.TotalCount, Is.EqualTo(1));
        Assert.That(result.Items.Single().Id, Is.EqualTo("room-1"));
    }

    [Test]
    public async Task SearchRoomsAsync_AllFilters_QueryMatchesRoomSearchRequest()
    {
        var (gateway, stub) = CreateGateway(_ => OkPage());
        var request = new RoomSearchRequest(
            CampusId: "ICTU",
            MinRentVnd: 1_000_000,
            MaxRentVnd: 3_000_000,
            MinAreaM2: 15.5,
            MaxAreaM2: 30.5,
            RoomTypes: new[] { RoomType.BoardingRoom, RoomType.MiniApartment },
            MaxDistanceKm: 2.5,
            Sort: "price_asc",
            PageNumber: 2,
            PageSize: 10);

        await gateway.SearchRoomsAsync(request);

        var query = HttpUtility.ParseQueryString(stub.Requests.Single().Uri.Query);
        Assert.That(query.Count, Is.EqualTo(10));
        Assert.Multiple(() =>
        {
            Assert.That(query["campusId"], Is.EqualTo("ICTU"));
            Assert.That(query["minRentVnd"], Is.EqualTo("1000000"));
            Assert.That(query["maxRentVnd"], Is.EqualTo("3000000"));
            Assert.That(query["minAreaM2"], Is.EqualTo("15.5"));
            Assert.That(query["maxAreaM2"], Is.EqualTo("30.5"));
            Assert.That(query["roomTypes"], Is.EqualTo("BoardingRoom,MiniApartment"));
            Assert.That(query["maxDistanceKm"], Is.EqualTo("2.5"));
            Assert.That(query["sort"], Is.EqualTo("price_asc"));
            Assert.That(query["pageNumber"], Is.EqualTo("2"));
            Assert.That(query["pageSize"], Is.EqualTo("10"));
        });
    }

    [Test]
    public async Task SearchRoomsAsync_NullFilters_AreOmitted()
    {
        var (gateway, stub) = CreateGateway(_ => OkPage());

        await gateway.SearchRoomsAsync(new RoomSearchRequest(CampusId: "ICTU"));

        var query = HttpUtility.ParseQueryString(stub.Requests.Single().Uri.Query);
        Assert.That(query.AllKeys, Is.EquivalentTo(new[] { "campusId", "pageNumber", "pageSize" }));
    }

    [Test]
    public async Task SearchRoomsAsync_RoomTypes_AreCommaSeparatedNames()
    {
        var (gateway, stub) = CreateGateway(_ => OkPage());

        await gateway.SearchRoomsAsync(new RoomSearchRequest(
            RoomTypes: new[] { RoomType.BoardingRoom, RoomType.WholeHouse, RoomType.Dormitory }));

        var query = HttpUtility.ParseQueryString(stub.Requests.Single().Uri.Query);
        Assert.That(query["roomTypes"], Is.EqualTo("BoardingRoom,WholeHouse,Dormitory"));
    }

    [Test]
    public async Task SearchRoomsAsync_ViVnCulture_UsesInvariantDecimalPoint()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("vi-VN");
            var (gateway, stub) = CreateGateway(_ => OkPage());

            await gateway.SearchRoomsAsync(new RoomSearchRequest(MinAreaM2: 12.5, MaxDistanceKm: 1.5));

            var query = HttpUtility.ParseQueryString(stub.Requests.Single().Uri.Query);
            Assert.That(query["maxDistanceKm"], Is.EqualTo("1.5"));
            Assert.That(query["minAreaM2"], Is.EqualTo("12.5"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Test]
    public async Task SearchRoomsAsync_EnumStrings_Deserialize()
    {
        const string json = """
            {"items":[{"id":"room-1","title":"Test room","monthlyRentVnd":2000000,"areaM2":20,
            "roomType":"BoardingRoom","distanceKm":0.5,"isVerified":true,"status":"Available",
            "coverThumbUrl":null,"latitude":21.5851,"longitude":105.8066,
            "publishedAtUtc":"2026-01-01T00:00:00Z","isFavourite":false}],
            "totalCount":1,"pageNumber":1,"pageSize":20}
            """;
        var (gateway, _) = CreateGateway(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var result = await gateway.SearchRoomsAsync(new RoomSearchRequest());

        var card = result.Items.Single();
        Assert.That(card.RoomType, Is.EqualTo(RoomType.BoardingRoom));
        Assert.That(card.Status, Is.EqualTo(ListingStatus.Available));
    }

    [TestCase(ProblemCodes.InvalidSortOption)]
    [TestCase(ProblemCodes.CampusNotFound)]
    public void SearchRoomsAsync_Problem400_ThrowsApiExceptionWithErrorCode(string errorCode)
    {
        var (gateway, _) = CreateGateway(_ => Problem(HttpStatusCode.BadRequest, errorCode));

        var ex = Assert.ThrowsAsync<ApiException>(async () => await gateway.SearchRoomsAsync(new RoomSearchRequest()));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(ex.ErrorCode, Is.EqualTo(errorCode));
    }

    [Test]
    public void SearchRoomsAsync_Problem500_ThrowsApiException()
    {
        var (gateway, _) = CreateGateway(_ => Problem(HttpStatusCode.InternalServerError, ProblemCodes.InternalError));

        var ex = Assert.ThrowsAsync<ApiException>(async () => await gateway.SearchRoomsAsync(new RoomSearchRequest()));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
    }

    [Test]
    public void SearchRoomsAsync_EmptyBody_ThrowsApiException()
    {
        var (gateway, _) = CreateGateway(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        });

        var ex = Assert.ThrowsAsync<ApiException>(async () => await gateway.SearchRoomsAsync(new RoomSearchRequest()));

        Assert.That(ex!.Message, Is.EqualTo("Server returned an empty body."));
    }

    [Test]
    public void SearchRoomsAsync_NullRequest_Throws()
    {
        var (gateway, _) = CreateGateway(_ => OkPage());

        Assert.ThrowsAsync<ArgumentNullException>(async () => await gateway.SearchRoomsAsync(null!));
    }

    [Test]
    public void SearchRoomsAsync_CancelledToken_Cancels()
    {
        var (gateway, stub) = CreateGateway(_ => OkPage());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await gateway.SearchRoomsAsync(new RoomSearchRequest(), cts.Token));
        Assert.That(stub.Requests, Is.Empty);
    }

    [Test]
    public async Task SearchRoomsAsync_SortEmptyString_IsSentAsEmpty()
    {
        var (gateway, stub) = CreateGateway(_ => OkPage());

        await gateway.SearchRoomsAsync(new RoomSearchRequest(Sort: string.Empty));

        var recorded = stub.Requests.Single();
        Assert.That(recorded.Uri.Query, Does.Contain("sort="));
        var query = HttpUtility.ParseQueryString(recorded.Uri.Query);
        Assert.That(query["sort"], Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task GetRoomDetailAsync_ValidRoomId_CallsEndpointAndReturnsDetail()
    {
        var expectedDetail = Detail("room-123");
        var (gateway, stub) = CreateGateway(_ => JsonResponse(HttpStatusCode.OK, expectedDetail));

        var result = await gateway.GetRoomDetailAsync("room-123");

        var recorded = stub.Requests.Single();
        Assert.That(recorded.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(recorded.Path, Is.EqualTo("/api/rooms/room-123"));
        Assert.That(result.Id, Is.EqualTo("room-123"));
        Assert.That(result.Title, Is.EqualTo(expectedDetail.Title));
    }

    [Test]
    public async Task GetRoomDetailAsync_WithCampusId_AppendsEscapedCampusId()
    {
        var expectedDetail = Detail("room-123");
        var (gateway, stub) = CreateGateway(_ => JsonResponse(HttpStatusCode.OK, expectedDetail));

        await gateway.GetRoomDetailAsync("room-123", "ICTU Campus");

        var recorded = stub.Requests.Single();
        Assert.That(recorded.Path, Is.EqualTo("/api/rooms/room-123"));
        Assert.That(recorded.Uri.Query, Is.EqualTo("?campusId=ICTU%20Campus"));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void GetRoomDetailAsync_InvalidRoomId_ThrowsArgumentException(string invalidId)
    {
        var (gateway, _) = CreateGateway(_ => JsonResponse(HttpStatusCode.OK, Detail("room-1")));

        Assert.ThrowsAsync<ArgumentException>(async () => await gateway.GetRoomDetailAsync(invalidId));
    }

    [Test]
    public void GetRoomDetailAsync_RoomNotFound_ThrowsApiException()
    {
        var (gateway, _) = CreateGateway(_ => Problem(HttpStatusCode.NotFound, ProblemCodes.RoomNotFound));

        var ex = Assert.ThrowsAsync<ApiException>(async () => await gateway.GetRoomDetailAsync("non-existent"));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.RoomNotFound));
    }

    [Test]
    public async Task GetSimilarRoomsAsync_ValidRoomId_CallsEndpointAndReturnsCards()
    {
        var cards = new List<RoomCardDto> { Card() };
        var (gateway, stub) = CreateGateway(_ => JsonResponse(HttpStatusCode.OK, cards));

        var result = await gateway.GetSimilarRoomsAsync("room-123");

        var recorded = stub.Requests.Single();
        Assert.That(recorded.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(recorded.Path, Is.EqualTo("/api/rooms/room-123/similar"));
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo("room-1"));
    }

    [Test]
    public async Task GetSimilarRoomsAsync_WithCampusId_AppendsEscapedCampusId()
    {
        var cards = new List<RoomCardDto> { Card() };
        var (gateway, stub) = CreateGateway(_ => JsonResponse(HttpStatusCode.OK, cards));

        await gateway.GetSimilarRoomsAsync("room-123", "TNUE");

        var recorded = stub.Requests.Single();
        Assert.That(recorded.Path, Is.EqualTo("/api/rooms/room-123/similar"));
        Assert.That(recorded.Uri.Query, Is.EqualTo("?campusId=TNUE"));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void GetSimilarRoomsAsync_InvalidRoomId_ThrowsArgumentException(string invalidId)
    {
        var (gateway, _) = CreateGateway(_ => JsonResponse(HttpStatusCode.OK, new List<RoomCardDto>()));

        Assert.ThrowsAsync<ArgumentException>(async () => await gateway.GetSimilarRoomsAsync(invalidId));
    }

    [Test]
    public async Task GetRoomTravelTimesAsync_ValidRoomId_CallsEndpointAndDeserializesModes()
    {
        var expected = new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 300, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1080, true),
                new TravelTimeItemDto(TravelMode.Walking, 1620, 1180, false)
            ]);
        var (gateway, stub) = CreateGateway(_ => JsonResponse(HttpStatusCode.OK, expected));

        var result = await gateway.GetRoomTravelTimesAsync("room-123");

        var recorded = stub.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(recorded.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(recorded.Path, Is.EqualTo("/api/rooms/room-123/travel-times"));
            Assert.That(recorded.Uri.Query, Is.Empty);
            Assert.That(result.CampusId, Is.EqualTo("ICTU"));
            Assert.That(result.IsFallback, Is.False);
            Assert.That(result.StraightLineDistanceKm, Is.EqualTo(1.2));
            Assert.That(result.Items, Is.EqualTo(expected.Items));
        });
    }

    [Test]
    public async Task GetRoomTravelTimesAsync_WithCampusId_AppendsEscapedCampusId()
    {
        var expected = new TravelTimesDto("ICTU Campus", 1.2, true, Array.Empty<TravelTimeItemDto>());
        var (gateway, stub) = CreateGateway(_ => JsonResponse(HttpStatusCode.OK, expected));

        await gateway.GetRoomTravelTimesAsync("room-123", "ICTU Campus");

        var recorded = stub.Requests.Single();
        Assert.That(recorded.Path, Is.EqualTo("/api/rooms/room-123/travel-times"));
        Assert.That(recorded.Uri.Query, Is.EqualTo("?campusId=ICTU%20Campus"));
    }

    [Test]
    public async Task GetRoomTravelTimesAsync_FallbackResponse_DeserializesEmptyItems()
    {
        const string json = """
            {"campusId":"ICTU","straightLineDistanceKm":1.2,"isFallback":true,"items":[]}
            """;
        var (gateway, _) = CreateGateway(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var result = await gateway.GetRoomTravelTimesAsync("room-123");

        Assert.Multiple(() =>
        {
            Assert.That(result.CampusId, Is.EqualTo("ICTU"));
            Assert.That(result.IsFallback, Is.True);
            Assert.That(result.StraightLineDistanceKm, Is.EqualTo(1.2));
            Assert.That(result.Items, Is.Empty);
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public void GetRoomTravelTimesAsync_InvalidRoomId_ThrowsArgumentException(string invalidId)
    {
        var (gateway, _) = CreateGateway(_ => JsonResponse(HttpStatusCode.OK,
            new TravelTimesDto("ICTU", 0, true, Array.Empty<TravelTimeItemDto>())));

        Assert.ThrowsAsync<ArgumentException>(async () => await gateway.GetRoomTravelTimesAsync(invalidId));
    }

    [TestCase(HttpStatusCode.NotFound, ProblemCodes.RoomNotFound)]
    [TestCase(HttpStatusCode.BadRequest, ProblemCodes.CampusNotFound)]
    public void GetRoomTravelTimesAsync_ProblemResponse_ThrowsApiExceptionWithErrorCode(HttpStatusCode status, string errorCode)
    {
        var (gateway, _) = CreateGateway(_ => Problem(status, errorCode));

        var ex = Assert.ThrowsAsync<ApiException>(async () => await gateway.GetRoomTravelTimesAsync("room-123", "NOPE"));

        Assert.That(ex!.StatusCode, Is.EqualTo(status));
        Assert.That(ex.ErrorCode, Is.EqualTo(errorCode));
    }

    private static (HttpRoomGateway Gateway, StubHttpMessageHandler Stub) CreateGateway(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var stub = new StubHttpMessageHandler(responder);
        var client = new HttpClient(stub) { BaseAddress = new Uri("http://localhost") };
        return (new HttpRoomGateway(client), stub);
    }

    private static RoomCardDto Card() => new(
        "room-1", "Test room", 2_000_000, 20, RoomType.BoardingRoom, 0.5, true,
        ListingStatus.Available, null, 21.5851, 105.8066,
        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), false);

    private static RoomDetailDto Detail(string id) => new(
        id, "Test Room", "Test description", RoomType.BoardingRoom, ListingStatus.Available, true,
        2_500_000, 2_500_000, 25.0, 2, Furnishing.Full, new DateOnly(2026, 12, 1),
        new AddressDto("12 Z115", "Quyet Thang", "Thai Nguyen"),
        new GeoPointDto(21.5851, 105.8066),
        1.2,
        new UtilitiesView(3500, 100000, WaterPriceUnit.PerPerson, null, 50000, null, 2950000),
        [Amenity.Wifi, Amenity.AirConditioner],
        new SafetyDto(4, true, [SafetyFeature.Cctv, SafetyFeature.SecureEntrance]),
        null,
        Array.Empty<PhotoDto>(),
        new LandlordContactDto("landlord-1", "Nguyen Thi Hoa", true, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), null, null),
        false,
        new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc));

    private static HttpResponseMessage OkPage(params RoomCardDto[] cards) =>
        JsonResponse(HttpStatusCode.OK, new PagedResult<RoomCardDto>(cards, cards.Length, 1, 20));

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