using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Controllers;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// RoomsController tests covering SearchRooms (US04), GetRoomById (US02), and GetSimilarRooms (US02 AC3).
/// </summary>
[TestFixture]
public class RoomsControllerTests
{
    private ISearchRoomsUseCase _useCase = null!;
    private IGetRoomDetailUseCase _getRoomDetailUseCase = null!;
    private IGetSimilarRoomsUseCase _getSimilarRoomsUseCase = null!;
    private RoomsController _controller = null!;
    private RoomSearchRequest? _capturedRequest;

    [SetUp]
    public void SetUp()
    {
        _useCase = Substitute.For<ISearchRoomsUseCase>();
        _getRoomDetailUseCase = Substitute.For<IGetRoomDetailUseCase>();
        _getSimilarRoomsUseCase = Substitute.For<IGetSimilarRoomsUseCase>();
        _controller = new RoomsController(_useCase, _getRoomDetailUseCase, _getSimilarRoomsUseCase);
        _capturedRequest = null;
    }

    private void ArrangeCapturingUseCase(PagedResult<RoomCardDto>? result = null)
    {
        _useCase
            .ExecuteAsync(Arg.Any<RoomSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                _capturedRequest = callInfo.Arg<RoomSearchRequest>();
                return Task.FromResult(result ?? CreatePagedResult());
            });
    }

    private static PagedResult<RoomCardDto> CreatePagedResult() => new(
        new[]
        {
            new RoomCardDto(
                Id: "room-1",
                Title: "Phòng trọ gần ICTU",
                MonthlyRentVnd: 1_800_000L,
                AreaM2: 20,
                RoomType: RoomType.BoardingRoom,
                DistanceKm: 0.8,
                IsVerified: true,
                Status: ListingStatus.Available,
                CoverThumbUrl: null,
                Latitude: 21.5851,
                Longitude: 105.8066,
                PublishedAtUtc: null,
                IsFavourite: false)
        },
        TotalCount: 1,
        PageNumber: 1,
        PageSize: 20);

    // ----------------------------------------------------------------- routing & attribute (LOW)

    [Test]
    public void Controller_HasRouteApiRooms()
    {
        var route = typeof(RoomsController).GetCustomAttribute<RouteAttribute>();

        Assert.Multiple(() =>
        {
            Assert.That(route, Is.Not.Null);
            Assert.That(route!.Template, Is.EqualTo("api/[controller]"));
        });
    }

    [Test]
    public void SearchRooms_Action_HasHttpGetAndAllowAnonymous()
    {
        var action = typeof(RoomsController).GetMethod(nameof(RoomsController.SearchRooms));

        Assert.That(action, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(action!.GetCustomAttribute<HttpGetAttribute>(), Is.Not.Null);
            Assert.That(action.GetCustomAttribute<AllowAnonymousAttribute>(), Is.Not.Null,
                "[AllowAnonymous] MUST sit on the GET action, not on the controller (review LOW).");
        });
    }

    [Test]
    public void Controller_DoesNotDeclareAllowAnonymousAtClassLevel()
    {
        var classLevel = typeof(RoomsController).GetCustomAttribute<AllowAnonymousAttribute>();

        Assert.That(classLevel, Is.Null, "Đặt [AllowAnonymous] ở class sẽ mở khoá cả các action ghi sau này.");
    }

    [Test]
    public void Constructor_WhenUseCaseIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RoomsController((ISearchRoomsUseCase)null!));
        Assert.Throws<ArgumentNullException>(() => new RoomsController((IGetRoomDetailUseCase)null!));
    }

    // ----------------------------------------------------------------- clamp & forwarding

    [Test]
    public void ConfiguredPageSize_MatchesOpenApiDefaultOf20()
    {
        Assert.That(new RoomSearchRequest().PageSize, Is.EqualTo(20));
    }

    [Test]
    public async Task SearchRooms_ForwardsRequest_ReturnsOkWithPagedResult()
    {
        var request = new RoomSearchRequest(MaxRentVnd: 2_000_000L, PageNumber: 1, PageSize: 2);
        var expected = CreatePagedResult();
        _useCase.ExecuteAsync(Arg.Any<RoomSearchRequest>(), Arg.Any<CancellationToken>()).Returns(expected);

        var response = await _controller.SearchRooms(request, CancellationToken.None);
        var okResult = response.Result as OkObjectResult;

        Assert.Multiple(() =>
        {
            Assert.That(okResult, Is.Not.Null);
            Assert.That(okResult!.StatusCode, Is.EqualTo(200));
            Assert.That(okResult.Value, Is.SameAs(expected));
        });
    }

    [Test]
    public async Task SearchRooms_ClampsPageSizeAbove50()
    {
        ArrangeCapturingUseCase();

        await _controller.SearchRooms(new RoomSearchRequest(PageSize: 100), CancellationToken.None);

        Assert.That(_capturedRequest!.PageSize, Is.EqualTo(50));
    }

    [Test]
    public async Task SearchRooms_ClampsPageSizeBelow1()
    {
        ArrangeCapturingUseCase();

        await _controller.SearchRooms(new RoomSearchRequest(PageSize: 0), CancellationToken.None);

        Assert.That(_capturedRequest!.PageSize, Is.EqualTo(1));
    }

    [Test]
    public async Task SearchRooms_ClampsPageNumberBelow1()
    {
        ArrangeCapturingUseCase();

        await _controller.SearchRooms(new RoomSearchRequest(PageNumber: 0), CancellationToken.None);

        Assert.That(_capturedRequest!.PageNumber, Is.EqualTo(1));
    }

    [Test]
    public async Task SearchRooms_PassesFiltersThrough()
    {
        ArrangeCapturingUseCase();
        var request = new RoomSearchRequest(
            CampusId: "ICTU",
            MinRentVnd: 1_000_000L,
            MaxRentVnd: 2_000_000L,
            MinAreaM2: 15,
            MaxAreaM2: 30,
            RoomTypes: new[] { RoomType.BoardingRoom, RoomType.MiniApartment },
            MaxDistanceKm: 2.5,
            Sort: "price_asc");

        await _controller.SearchRooms(request, CancellationToken.None);

        Assert.That(_capturedRequest, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(_capturedRequest!.CampusId, Is.EqualTo("ICTU"));
            Assert.That(_capturedRequest.MinRentVnd, Is.EqualTo(1_000_000L));
            Assert.That(_capturedRequest.MaxRentVnd, Is.EqualTo(2_000_000L));
            Assert.That(_capturedRequest.MinAreaM2, Is.EqualTo(15));
            Assert.That(_capturedRequest.MaxAreaM2, Is.EqualTo(30));
            Assert.That(_capturedRequest.RoomTypes, Is.EquivalentTo(new[] { RoomType.BoardingRoom, RoomType.MiniApartment }));
            Assert.That(_capturedRequest.MaxDistanceKm, Is.EqualTo(2.5));
            Assert.That(_capturedRequest.Sort, Is.EqualTo("price_asc"));
        });
    }

    [Test]
    public async Task SearchRooms_ForwardsCancellationToken()
    {
        ArrangeCapturingUseCase();
        using var cts = new CancellationTokenSource();

        await _controller.SearchRooms(new RoomSearchRequest(), cts.Token);

        await _useCase.Received(1).ExecuteAsync(
            Arg.Any<RoomSearchRequest>(),
            Arg.Is<CancellationToken>(token => token == cts.Token));
    }

    [Test]
    public async Task SearchRooms_DoesNotSwallowDomainValidationException()
    {
        _useCase.ExecuteAsync(Arg.Any<RoomSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<PagedResult<RoomCardDto>>>(_ => throw new DomainValidationException("Invalid filters.", "INVALID_SORT_OPTION"));

        await Assert.ThatAsync(
            async () => await _controller.SearchRooms(new RoomSearchRequest(), CancellationToken.None),
            Throws.TypeOf<DomainValidationException>());
    }

    // ----------------------------------------------------------------- US02: getRoomDetail & getSimilarRooms

    [Test]
    public async Task GetRoomById_WhenRoomExists_Returns200OkWithRoomDetailDto()
    {
        var sampleDto = CreateSampleDetailDto();

        _getRoomDetailUseCase.ExecuteAsync("room-001", Arg.Any<CancellationToken>(), null)
            .Returns(sampleDto);

        var actionResult = await _controller.GetRoomById("room-001", CancellationToken.None);

        Assert.That(actionResult, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)actionResult;
        Assert.That(okResult.Value, Is.EqualTo(sampleDto));
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task GetRoomById_WhenIdIsNullOrWhitespace_Returns400BadRequestWithProblemDetails(string invalidId)
    {
        var actionResult = await _controller.GetRoomById(invalidId, CancellationToken.None);

        Assert.That(actionResult, Is.InstanceOf<BadRequestObjectResult>());
        var badRequestResult = (BadRequestObjectResult)actionResult;
        Assert.That(badRequestResult.Value, Is.InstanceOf<ProblemDetails>());

        var problemDetails = (ProblemDetails)badRequestResult.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(problemDetails.Status, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(problemDetails.Title, Is.EqualTo("Invalid room request"));
            Assert.That(problemDetails.Detail, Does.Contain("Room ID cannot be empty or whitespace."));
            Assert.That(problemDetails.Extensions["errorCode"]?.ToString(), Is.EqualTo("VALIDATION_FAILED"));
        });
    }

    [Test]
    public async Task GetRoomById_WhenRoomNotFound_Returns404NotFoundWithProblemDetails()
    {
        _getRoomDetailUseCase.ExecuteAsync("non-existent-id", Arg.Any<CancellationToken>(), null)
            .Returns((RoomDetailDto?)null);

        var actionResult = await _controller.GetRoomById("non-existent-id", CancellationToken.None);

        Assert.That(actionResult, Is.InstanceOf<NotFoundObjectResult>());
        var notFoundResult = (NotFoundObjectResult)actionResult;
        Assert.That(notFoundResult.Value, Is.InstanceOf<ProblemDetails>());

        var problemDetails = (ProblemDetails)notFoundResult.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(problemDetails.Status, Is.EqualTo(StatusCodes.Status404NotFound));
            Assert.That(problemDetails.Title, Is.EqualTo("Room not found"));
            Assert.That(problemDetails.Detail, Does.Contain("Room listing with ID 'non-existent-id' was not found."));
            Assert.That(problemDetails.Extensions["errorCode"]?.ToString(), Is.EqualTo("ROOM_NOT_FOUND"));
        });
    }

    [Test]
    public async Task GetRoomById_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        _getRoomDetailUseCase.ExecuteAsync("room-001", token, null).Returns((RoomDetailDto?)null);

        await _controller.GetRoomById("room-001", token);

        await _getRoomDetailUseCase.Received(1).ExecuteAsync("room-001", token, null);
    }

    [Test]
    public async Task GetSimilarRooms_WhenRoomExists_Returns200OkWithSimilarRooms()
    {
        var targetRoom = CreateSampleDetailDto("room-001");
        _getRoomDetailUseCase.ExecuteAsync("room-001", Arg.Any<CancellationToken>(), null).Returns(targetRoom);

        var similarCards = new List<RoomCardDto>
        {
            new("sim-1", "Phòng 1", 1500000L, 20.0, RoomType.BoardingRoom, 0.5, true, ListingStatus.Available, null, 21.58, 105.80, null, false)
        };
        _getSimilarRoomsUseCase.ExecuteAsync("room-001", null, Arg.Any<CancellationToken>()).Returns(similarCards);

        var result = await _controller.GetSimilarRooms("room-001", CancellationToken.None);

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result;
        Assert.That(okResult.Value, Is.EqualTo(similarCards));
    }

    [Test]
    public async Task GetSimilarRooms_WhenTargetRoomNotFound_Returns404NotFound()
    {
        _getRoomDetailUseCase.ExecuteAsync("room-not-found", Arg.Any<CancellationToken>(), null).Returns((RoomDetailDto?)null);

        var result = await _controller.GetSimilarRooms("room-not-found", CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        var notFound = (NotFoundObjectResult)result;
        var problem = (ProblemDetails)notFound.Value!;
        Assert.That(problem.Extensions["errorCode"]?.ToString(), Is.EqualTo("ROOM_NOT_FOUND"));
    }

    private static RoomDetailDto CreateSampleDetailDto(
        string id = "room-001",
        string title = "Phòng trọ đẹp",
        ListingStatus status = ListingStatus.Available)
    {
        return new RoomDetailDto(
            Id: id,
            Title: title,
            Description: "Mô tả",
            RoomType: RoomType.BoardingRoom,
            Status: status,
            IsVerified: true,
            MonthlyRentVnd: 1500000m,
            DepositVnd: 1500000m,
            AreaM2: 20.0,
            Address: new AddressDto("Số 12 Đường Z115", "Quyết Thắng", "TP. Thái Nguyên"),
            Location: new GeoPointDto(21.5875, 105.8080),
            DistanceKm: 0.5,
            Utilities: new UtilitiesView(3500m, 25000m, "PerCubicMeter", 100000m, 0m, null, 1950000),
            Amenities: new[] { Amenity.AirConditioner, Amenity.Wifi },
            Safety: new SafetyDto(4, true, new[] { "Cctv", "SecureEntrance" }),
            Photos: new[] { new PhotoDto("p1", "Standard", "thumb.jpg", "medium.jpg", "orig.jpg", 0) },
            Landlord: new LandlordContactDto("landlord-1", "Chủ nhà", true, DateTime.UtcNow, "0912345678", "https://zalo.me/0912345678"),
            LastUpdatedAtUtc: DateTime.UtcNow
        );
    }
}
