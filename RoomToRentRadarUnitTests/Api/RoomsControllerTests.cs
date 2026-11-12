using System.Reflection;
using Microsoft.AspNetCore.Authorization;
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
/// T04.1 / US04: <c>operationId: searchRooms</c>. RED ở Task 1 cho phần chuyển <c>[AllowAnonymous]</c>
/// từ class xuống action (LOW của review); Task 4 (Giai đoạn 4) làm xanh.
/// </summary>
[TestFixture]
public class RoomsControllerTests
{
    private ISearchRoomsUseCase _useCase = null!;
    private RoomsController _controller = null!;
    private RoomSearchRequest? _capturedRequest;

    [SetUp]
    public void SetUp()
    {
        _useCase = Substitute.For<ISearchRoomsUseCase>();
        _controller = new RoomsController(_useCase);
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
    public void SearchRooms_DoesNotSwallowDomainValidationException()
    {
        _useCase.ExecuteAsync(Arg.Any<RoomSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<PagedResult<RoomCardDto>>>(_ => throw new DomainValidationException("Invalid filters.", "INVALID_SORT_OPTION"));

        Assert.ThrowsAsync<DomainValidationException>(
            async () => await _controller.SearchRooms(new RoomSearchRequest(), CancellationToken.None));
    }
}
