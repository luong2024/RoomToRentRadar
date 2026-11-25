namespace RoomToRentRadar.Adapters.Tests;

using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;

[TestFixture]
public class RoomListingsControllerTests
{
    private FakeGetRoomListingsUseCase _useCase = null!;
    private RoomListingsController _controller = null!;

    [SetUp]
    public void Setup()
    {
        _useCase = new FakeGetRoomListingsUseCase();
        _controller = new RoomListingsController(_useCase);
    }

    [Test]
    public async Task GetRoomsAsync_WhenSuccessful_ShouldReturnOkWithPagedResult()
    {
        var sampleDto = new RoomListingDto(
            "room-01",
            "Cozy Room",
            2000000,
            "VND",
            22.0,
            0.5,
            true,
            "123 Street",
            null,
            "campus-ictu");

        _useCase.ResultToReturn = new PagedResult<RoomListingDto>([sampleDto], 1, 20, 1);

        var query = new GetRoomListingsQuery("campus-ictu");
        var actionResult = await _controller.GetRoomsAsync(query, CancellationToken.None);

        Assert.That(actionResult, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)actionResult;
        Assert.That(okResult.Value, Is.InstanceOf<PagedResult<RoomListingDto>>());
        var pagedResult = (PagedResult<RoomListingDto>)okResult.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(pagedResult.TotalCount, Is.EqualTo(1));
            Assert.That(pagedResult.Items[0].Title, Is.EqualTo("Cozy Room"));
        });
    }

    [Test]
    public async Task GetRoomsAsync_WhenValidationFails_ShouldReturnBadRequestProblemDetails()
    {
        _useCase.ExceptionToThrow = new ValidationException("CampusId", "Campus ID is required.");

        var query = new GetRoomListingsQuery(string.Empty);
        var actionResult = await _controller.GetRoomsAsync(query, CancellationToken.None);

        Assert.That(actionResult, Is.InstanceOf<BadRequestObjectResult>());
        var badRequestResult = (BadRequestObjectResult)actionResult;
        Assert.That(badRequestResult.Value, Is.InstanceOf<ValidationProblemDetails>());
    }

    [Test]
    public async Task GetRoomsAsync_WhenSortByIsInvalid_ShouldReturnBadRequest()
    {
        _useCase.ExceptionToThrow = new ValidationException("SortBy", "SortBy must be one of: distance, price, area.");

        var query = new GetRoomListingsQuery("campus-ictu", SortBy: "invalid");
        var actionResult = await _controller.GetRoomsAsync(query, CancellationToken.None);

        Assert.That(actionResult, Is.InstanceOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)actionResult;
        var details = (ValidationProblemDetails)badRequest.Value!;
        Assert.That(details.Errors.ContainsKey("SortBy"), Is.True);
    }

    [Test]
    public async Task GetRoomsAsync_WhenSortOrderIsInvalid_ShouldReturnBadRequest()
    {
        _useCase.ExceptionToThrow = new ValidationException("SortOrder", "SortOrder must be either 'asc' or 'desc'.");

        var query = new GetRoomListingsQuery("campus-ictu", SortOrder: "descending");
        var actionResult = await _controller.GetRoomsAsync(query, CancellationToken.None);

        Assert.That(actionResult, Is.InstanceOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)actionResult;
        var details = (ValidationProblemDetails)badRequest.Value!;
        Assert.That(details.Errors.ContainsKey("SortOrder"), Is.True);
    }

    [Test]
    public async Task GetRoomsAsync_WhenCampusNotFound_ShouldReturnNotFoundProblemDetails()
    {
        _useCase.ExceptionToThrow = new CampusNotFoundException("unknown-campus");

        var query = new GetRoomListingsQuery("unknown-campus");
        var actionResult = await _controller.GetRoomsAsync(query, CancellationToken.None);

        Assert.That(actionResult, Is.InstanceOf<NotFoundObjectResult>());
        var notFoundResult = (NotFoundObjectResult)actionResult;
        Assert.That(notFoundResult.Value, Is.InstanceOf<ProblemDetails>());
    }

    [Test]
    public async Task SpatialSearchAsync_WhenSuccessful_ShouldReturnOkWithPagedResult()
    {
        var spatialUseCase = new FakeSearchRoomsInPolygonUseCase();
        var sampleDto = new SpatialRoomSearchResultDto(
            "room-01",
            "Cozy Studio",
            2500000m,
            "VND",
            25.0,
            "Thai Nguyen",
            21.5855,
            105.8075,
            true,
            null,
            "campus-ictu");

        spatialUseCase.ResultToReturn = new PagedResult<SpatialRoomSearchResultDto>([sampleDto], 1, 10, 1);
        var controller = new RoomListingsController(_useCase, spatialUseCase);

        var request = new SpatialRoomSearchRequestDto
        {
            PolygonCoordinates = [new(21.585, 105.807), new(21.586, 105.807), new(21.585, 105.808)],
            PageIndex = 1,
            PageSize = 10
        };

        var actionResult = await controller.SpatialSearchAsync(request, CancellationToken.None);

        Assert.That(actionResult, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)actionResult;
        Assert.That(okResult.Value, Is.InstanceOf<PagedResult<SpatialRoomSearchResultDto>>());
        var pagedResult = (PagedResult<SpatialRoomSearchResultDto>)okResult.Value!;
        Assert.That(pagedResult.TotalCount, Is.EqualTo(1));
        Assert.That(pagedResult.Items[0].Latitude, Is.EqualTo(21.5855));
    }

    [Test]
    public async Task SpatialSearchAsync_WhenValidationFails_ShouldReturnBadRequestProblemDetails()
    {
        var spatialUseCase = new FakeSearchRoomsInPolygonUseCase
        {
            ExceptionToThrow = new ValidationException("PolygonCoordinates", "A polygon must contain at least 3 vertices.")
        };
        var controller = new RoomListingsController(_useCase, spatialUseCase);

        var request = new SpatialRoomSearchRequestDto();
        var actionResult = await controller.SpatialSearchAsync(request, CancellationToken.None);

        Assert.That(actionResult, Is.InstanceOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)actionResult;
        Assert.That(badRequest.Value, Is.InstanceOf<ValidationProblemDetails>());
    }

    private sealed class FakeSearchRoomsInPolygonUseCase : ISearchRoomsInPolygonUseCase
    {
        public PagedResult<SpatialRoomSearchResultDto>? ResultToReturn { get; set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task<PagedResult<SpatialRoomSearchResultDto>> ExecuteAsync(
            SpatialRoomSearchRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ResultToReturn ?? new PagedResult<SpatialRoomSearchResultDto>([], 1, 10, 0));
        }
    }

    private sealed class FakeGetRoomListingsUseCase : IGetRoomListingsUseCase
    {
        public PagedResult<RoomListingDto>? ResultToReturn { get; set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task<PagedResult<RoomListingDto>> ExecuteAsync(GetRoomListingsQuery query, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ResultToReturn ?? new PagedResult<RoomListingDto>([], 1, 20, 0));
        }
    }
}
