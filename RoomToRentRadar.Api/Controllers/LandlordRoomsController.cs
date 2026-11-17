using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Authorization;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Responses.Landlord;
using RoomToRentRadar.Ports.Inbound.UseCases.Landlord;

namespace RoomToRentRadar.Api.Controllers;

/// <summary>
/// Landlord listing CRUD and state transitions (UC-30..UC-33). Reads and unpublish-family actions accept
/// any authenticated landlord; publishing, republishing and resubmitting require a verified landlord
/// (BR-AUTH-03). Every mutation carries the optimistic-concurrency <c>If-Match</c> header (ADR-012).
/// </summary>
[ApiController]
[Route("api/landlord/rooms")]
[Authorize]
public sealed class LandlordRoomsController : ControllerBase
{
    private const string IfMatchHeaderName = "If-Match";

    private readonly IListMyListingsUseCase _listMyListingsUseCase;
    private readonly ICreateListingUseCase _createListingUseCase;
    private readonly IGetMyListingUseCase _getMyListingUseCase;
    private readonly IUpdateListingUseCase _updateListingUseCase;
    private readonly IMarkListingRentedUseCase _markListingRentedUseCase;
    private readonly IMarkListingAvailableUseCase _markListingAvailableUseCase;
    private readonly IUnpublishListingUseCase _unpublishListingUseCase;
    private readonly IRepublishListingUseCase _republishListingUseCase;
    private readonly IResubmitListingUseCase _resubmitListingUseCase;

    public LandlordRoomsController(
        IListMyListingsUseCase listMyListingsUseCase,
        ICreateListingUseCase createListingUseCase,
        IGetMyListingUseCase getMyListingUseCase,
        IUpdateListingUseCase updateListingUseCase,
        IMarkListingRentedUseCase markListingRentedUseCase,
        IMarkListingAvailableUseCase markListingAvailableUseCase,
        IUnpublishListingUseCase unpublishListingUseCase,
        IRepublishListingUseCase republishListingUseCase,
        IResubmitListingUseCase resubmitListingUseCase)
    {
        ArgumentNullException.ThrowIfNull(listMyListingsUseCase);
        ArgumentNullException.ThrowIfNull(createListingUseCase);
        ArgumentNullException.ThrowIfNull(getMyListingUseCase);
        ArgumentNullException.ThrowIfNull(updateListingUseCase);
        ArgumentNullException.ThrowIfNull(markListingRentedUseCase);
        ArgumentNullException.ThrowIfNull(markListingAvailableUseCase);
        ArgumentNullException.ThrowIfNull(unpublishListingUseCase);
        ArgumentNullException.ThrowIfNull(republishListingUseCase);
        ArgumentNullException.ThrowIfNull(resubmitListingUseCase);

        _listMyListingsUseCase = listMyListingsUseCase;
        _createListingUseCase = createListingUseCase;
        _getMyListingUseCase = getMyListingUseCase;
        _updateListingUseCase = updateListingUseCase;
        _markListingRentedUseCase = markListingRentedUseCase;
        _markListingAvailableUseCase = markListingAvailableUseCase;
        _unpublishListingUseCase = unpublishListingUseCase;
        _republishListingUseCase = republishListingUseCase;
        _resubmitListingUseCase = resubmitListingUseCase;
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.LandlordPolicy)]
    [ProducesResponseType(typeof(PagedMyListingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedMyListingsDto>> ListMyListings(
        [FromQuery] ListingStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var request = new ListMyListingsRequest(
            LandlordId: User.GetRequiredUserId(),
            Status: status,
            PageNumber: pageNumber,
            PageSize: pageSize);

        var result = await _listMyListingsUseCase.ExecuteAsync(request, ct);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.VerifiedLandlordPolicy)]
    [ProducesResponseType(typeof(MyListingDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MyListingDetailDto>> CreateListing(
        [FromBody] UpsertListingRequest request,
        CancellationToken ct)
    {
        var command = new CreateListingCommand(User.GetRequiredUserId(), request);
        var result = await _createListingUseCase.ExecuteAsync(command, ct);

        SetEtag(result.Version);
        Response.Headers.Location = $"/api/landlord/rooms/{result.Id}";
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("{roomId}")]
    [Authorize(Policy = AuthorizationPolicies.LandlordPolicy)]
    [ProducesResponseType(typeof(MyListingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyListingDetailDto>> GetMyListing(
        [FromRoute] string roomId,
        CancellationToken ct)
    {
        var result = await _getMyListingUseCase.ExecuteAsync(User.GetRequiredUserId(), roomId, ct);
        SetEtag(result.Version);
        return Ok(result);
    }

    [HttpPut("{roomId}")]
    [Authorize(Policy = AuthorizationPolicies.LandlordPolicy)]
    [ProducesResponseType(typeof(MyListingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult<MyListingDetailDto>> UpdateListing(
        [FromRoute] string roomId,
        [FromBody] UpsertListingRequest request,
        CancellationToken ct)
    {
        var command = new UpdateListingCommand(
            LandlordId: User.GetRequiredUserId(),
            RoomId: roomId,
            IfMatch: GetIfMatchHeader(),
            Listing: request);

        var result = await _updateListingUseCase.ExecuteAsync(command, ct);
        SetEtag(result.Version);
        return Ok(result);
    }

    [HttpPost("{roomId}/mark-rented")]
    [Authorize(Policy = AuthorizationPolicies.LandlordPolicy)]
    [ProducesResponseType(typeof(MyListingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public Task<ActionResult<MyListingDetailDto>> MarkListingRented(
        [FromRoute] string roomId,
        CancellationToken ct) =>
        ExecuteTransitionAsync(_markListingRentedUseCase.ExecuteAsync, roomId, ct);

    [HttpPost("{roomId}/mark-available")]
    [Authorize(Policy = AuthorizationPolicies.LandlordPolicy)]
    [ProducesResponseType(typeof(MyListingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public Task<ActionResult<MyListingDetailDto>> MarkListingAvailable(
        [FromRoute] string roomId,
        CancellationToken ct) =>
        ExecuteTransitionAsync(_markListingAvailableUseCase.ExecuteAsync, roomId, ct);

    [HttpPost("{roomId}/unpublish")]
    [Authorize(Policy = AuthorizationPolicies.LandlordPolicy)]
    [ProducesResponseType(typeof(MyListingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public Task<ActionResult<MyListingDetailDto>> UnpublishListing(
        [FromRoute] string roomId,
        CancellationToken ct) =>
        ExecuteTransitionAsync(_unpublishListingUseCase.ExecuteAsync, roomId, ct);

    [HttpPost("{roomId}/republish")]
    [Authorize(Policy = AuthorizationPolicies.VerifiedLandlordPolicy)]
    [ProducesResponseType(typeof(MyListingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public Task<ActionResult<MyListingDetailDto>> RepublishListing(
        [FromRoute] string roomId,
        CancellationToken ct) =>
        ExecuteTransitionAsync(_republishListingUseCase.ExecuteAsync, roomId, ct);

    [HttpPost("{roomId}/resubmit")]
    [Authorize(Policy = AuthorizationPolicies.VerifiedLandlordPolicy)]
    [ProducesResponseType(typeof(MyListingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public Task<ActionResult<MyListingDetailDto>> ResubmitListing(
        [FromRoute] string roomId,
        CancellationToken ct) =>
        ExecuteTransitionAsync(_resubmitListingUseCase.ExecuteAsync, roomId, ct);

    private async Task<ActionResult<MyListingDetailDto>> ExecuteTransitionAsync(
        Func<TransitionMyListingCommand, CancellationToken, Task<MyListingDetailDto>> executeAsync,
        string roomId,
        CancellationToken ct)
    {
        var command = new TransitionMyListingCommand(
            LandlordId: User.GetRequiredUserId(),
            RoomId: roomId,
            IfMatch: GetIfMatchHeader());

        var result = await executeAsync(command, ct);
        SetEtag(result.Version);
        return Ok(result);
    }

    private string? GetIfMatchHeader() =>
        Request.Headers.TryGetValue(IfMatchHeaderName, out var values) && values.Count > 0
            ? values[0]
            : null;

    private void SetEtag(int version) => Response.Headers.ETag = $"\"{version}\"";
}
