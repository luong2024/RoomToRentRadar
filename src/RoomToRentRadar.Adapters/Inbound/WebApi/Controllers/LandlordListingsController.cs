namespace RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Adapters.Inbound.WebApi.Authorization;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Application.Ports.Inbound;

[ApiController]
[Route("api/v1/landlord/listings")]
[Authorize(Policy = AuthorizationPolicies.LandlordOnly)]
[Produces("application/json")]
public sealed class LandlordListingsController : ControllerBase
{
    // The mobile form allows 20 images at up to 10 MiB each, plus multipart fields and boundaries.
    private const long MaxRequestSizeBytes = 210L * 1024 * 1024;
    private readonly ICreateListingUseCase _createListing;
    private readonly IUpdateListingUseCase _updateListing;
    private readonly IUnpublishListingUseCase _unpublishListing;

    public LandlordListingsController(
        ICreateListingUseCase createListing,
        IUpdateListingUseCase updateListing,
        IUnpublishListingUseCase unpublishListing)
    {
        _createListing = createListing;
        _updateListing = updateListing;
        _unpublishListing = unpublishListing;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSizeBytes)]
    [ProducesResponseType(typeof(ListingWriteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create([FromForm] ListingFormRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetLandlordId(out var ownerId)) return Unauthorized();
        var result = await _createListing.ExecuteAsync(ownerId, ToCommand(request, []), cancellationToken);
        var response = new ListingWriteResponse(result.ListingId, result.ListingId, result.Status, result.ImageUrls);
        return Created($"/api/rooms/{result.ListingId}", response);
    }

    [HttpPut("{listingId}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSizeBytes)]
    [ProducesResponseType(typeof(ListingWriteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string listingId, [FromForm] ListingFormRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetLandlordId(out var ownerId)) return Unauthorized();
        var result = await _updateListing.ExecuteAsync(ownerId, listingId, ToCommand(request, request.ImageUrlsToRetain), cancellationToken);
        return Ok(new ListingWriteResponse(result.ListingId, result.ListingId, result.Status, result.ImageUrls));
    }

    [HttpPost("{listingId}/unpublish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpublish(string listingId, CancellationToken cancellationToken)
    {
        if (!TryGetLandlordId(out var ownerId)) return Unauthorized();
        await _unpublishListing.ExecuteAsync(ownerId, listingId, cancellationToken);
        return NoContent();
    }

    private static ListingWriteRequest ToCommand(ListingFormRequest request, IReadOnlyList<string>? retainedUrls)
    {
        var photos = request.Photos.Select(file => new ListingImageUpload(
            file.OpenReadStream,
            file.FileName,
            file.ContentType,
            file.Length)).ToList();
        return new ListingWriteRequest(
            request.Title,
            request.Address,
            request.MonthlyRentVnd,
            request.DepositVnd,
            request.AreaM2,
            request.RoomType,
            request.Description,
            photos,
            retainedUrls);
    }

    private bool TryGetLandlordId(out Guid ownerId)
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out ownerId) && ownerId != Guid.Empty;
    }
}
