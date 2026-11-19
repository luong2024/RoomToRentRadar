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
