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