using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Controllers;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;

namespace RoomToRentRadarUnitTests.Api;

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
                Description: "Phòng sạch, có điều hòa",
                Price: 1_800_000m,
                Area: 20,
                Address: "Thái Nguyên",
                Latitude: 21.5851,
                Longitude: 105.8066,
                DistanceKm: 0.8,
                IsAvailable: true,
                IsVerified: true,
                CreatedAtUtc: DateTime.UtcNow)
        },
        TotalCount: 1,
        PageNumber: 1,
        PageSize: 10);

    [Test]
    public void Controller_HasAllowAnonymousAttribute()
    {
        var allowAnonymous = typeof(RoomsController).GetCustomAttribute<AllowAnonymousAttribute>();

        Assert.That(allowAnonymous, Is.Not.Null, "RoomsController must allow anonymous access.");
    }

    [Test]
    public void Controller_HasRouteApiRooms()
    {
        var route = typeof(RoomsController).GetCustomAttribute<RouteAttribute>();

        Assert.That(route, Is.Not.Null);
        Assert.That(route!.Template, Is.EqualTo("api/[controller]"));
    }

    [Test]
    public async Task SearchRooms_ForwardsRequest_ReturnsOkWithPagedResult()
    {
        var request = new RoomSearchRequest(MaxPrice: 2_000_000m, Page: 1, PageSize: 2);
        var expected = CreatePagedResult();
        _useCase.ExecuteAsync(Arg.Any<RoomSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(expected);

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

        Assert.That(_capturedRequest, Is.Not.Null);
        Assert.That(_capturedRequest!.PageSize, Is.EqualTo(50));
    }

    [Test]
    public async Task SearchRooms_ClampsPageSizeBelow1()
    {
        ArrangeCapturingUseCase();

        await _controller.SearchRooms(new RoomSearchRequest(PageSize: 0), CancellationToken.None);

        Assert.That(_capturedRequest, Is.Not.Null);
        Assert.That(_capturedRequest!.PageSize, Is.EqualTo(1));
    }

    [Test]
    public async Task SearchRooms_ClampsPageBelow1()
    {
        ArrangeCapturingUseCase();

        await _controller.SearchRooms(new RoomSearchRequest(Page: 0), CancellationToken.None);

        Assert.That(_capturedRequest, Is.Not.Null);
        Assert.That(_capturedRequest!.Page, Is.EqualTo(1));
    }

    [Test]
    public async Task SearchRooms_PassesFiltersAndDefaultsThrough()
    {
        ArrangeCapturingUseCase();
        var request = new RoomSearchRequest(
            MinPrice: 1_000_000m,
            MaxPrice: 2_000_000m,
            MinArea: 15,
            MaxArea: 30,
            Latitude: 21.5851,
            Longitude: 105.8066,
            MaxDistanceKm: 2.5,
            Keyword: "gần trường",
            SortBy: "price_asc");

        await _controller.SearchRooms(request, CancellationToken.None);

        Assert.That(_capturedRequest, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(_capturedRequest!.MinPrice, Is.EqualTo(request.MinPrice));
            Assert.That(_capturedRequest.MaxPrice, Is.EqualTo(request.MaxPrice));
            Assert.That(_capturedRequest.MinArea, Is.EqualTo(request.MinArea));
            Assert.That(_capturedRequest.MaxArea, Is.EqualTo(request.MaxArea));
            Assert.That(_capturedRequest.Latitude, Is.EqualTo(request.Latitude));
            Assert.That(_capturedRequest.Longitude, Is.EqualTo(request.Longitude));
            Assert.That(_capturedRequest.MaxDistanceKm, Is.EqualTo(request.MaxDistanceKm));
            Assert.That(_capturedRequest.Keyword, Is.EqualTo(request.Keyword));
            Assert.That(_capturedRequest.SortBy, Is.EqualTo(request.SortBy));
            Assert.That(_capturedRequest.Page, Is.EqualTo(1));
            Assert.That(_capturedRequest.PageSize, Is.EqualTo(10));
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
            Arg.Is<CancellationToken>(t => t == cts.Token));
    }

    [Test]
    public void SearchRooms_DoesNotSwallowDomainValidationException()
    {
        var request = new RoomSearchRequest();
        _useCase.ExecuteAsync(Arg.Any<RoomSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<PagedResult<RoomCardDto>>>(_ => throw new DomainValidationException("Invalid filters."));

        Assert.ThrowsAsync<DomainValidationException>(
            async () => await _controller.SearchRooms(request, CancellationToken.None));
    }
}
