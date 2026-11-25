namespace RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;

using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Adapters.Inbound.WebApi.Authorization;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;

[ApiController]
[Route("api/v1/landlord/listings")]
[Authorize(Policy = AuthorizationPolicies.LandlordOnly)]
[Produces("application/json")]
public sealed class LandlordListingsController : ControllerBase
{
    // Leave room for multipart fields and boundaries beyond the photo payload.
    private const long MaxRequestSizeBytes = ListingPhotoLimits.MaxCount * ListingPhotoLimits.MaxBytesPerPhoto + 10L * 1024 * 1024;
    private readonly IGetListingCoordinatesUseCase _getCoordinates;
    private readonly ICreateListingUseCase _createListing;
    private readonly IUpdateListingUseCase _updateListing;
    private readonly IUnpublishListingUseCase _unpublishListing;

    public LandlordListingsController(
        ICreateListingUseCase createListing,
        IUpdateListingUseCase updateListing,
        IUnpublishListingUseCase unpublishListing,
        IGetListingCoordinatesUseCase getCoordinates)
    {
        _createListing = createListing;
        _updateListing = updateListing;
        _unpublishListing = unpublishListing;
        _getCoordinates = getCoordinates;
    }

    [HttpGet("{listingId}")]
    [ProducesResponseType(typeof(ListingCoordinatesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCoordinates(string listingId, CancellationToken cancellationToken)
    {
        if (!TryGetLandlordId(out var ownerId)) return Unauthorized();
        var result = await _getCoordinates.ExecuteAsync(ownerId, listingId, cancellationToken);
        return Ok(new ListingCoordinatesResponse(result.ListingId, result.Location?.Latitude, result.Location?.Longitude));
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ListingPhotoLimits.MaxBytesPerPhoto)]
    [ProducesResponseType(typeof(ListingWriteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create([FromForm] ListingFormRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetLandlordId(out var ownerId)) return Unauthorized();
        var result = await _createListing.ExecuteAsync(ownerId, ToCommand(request, []), cancellationToken);
        var response = ToResponse(result);
        return Created($"/api/rooms/{result.ListingId}", response);
    }

    [HttpPut("{listingId}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ListingPhotoLimits.MaxBytesPerPhoto)]
    [ProducesResponseType(typeof(ListingWriteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(string listingId, [FromForm] ListingFormRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetLandlordId(out var ownerId)) return Unauthorized();
        if (request.ReplaceGallery && request.ImageUrlsToRetain is { Count: > 0 })
        {
            throw new RequestValidationException("imageUrlsToRetain", "Do not send retained images when replacing the gallery.");
        }

        IReadOnlyList<string>? retainedUrls = request.ReplaceGallery ? [] : request.ImageUrlsToRetain;
        var result = await _updateListing.ExecuteAsync(ownerId, listingId, ToCommand(request, retainedUrls), cancellationToken);
        return Ok(ToResponse(result));
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

    private ListingWriteRequest ToCommand(ListingFormRequest request, IReadOnlyList<string>? retainedUrls)
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
            retainedUrls,
            ParseCoordinate("latitude", request.Latitude),
            ParseCoordinate("longitude", request.Longitude));
    }

    private double? ParseCoordinate(string field, string? value)
    {
        // The scalar model binder can select the first value of a repeated multipart field.
        if (Request.HasFormContentType &&
            (Request.Form[field].Count > 1 || Request.Form.Files.GetFile(field) is not null))
        {
            throw new RequestValidationException(field, $"Send {field} once as a text field.");
        }

        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Contains('\0') || !double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate))
        {
            throw new RequestValidationException(field, $"Enter {field} as a number with a period as the decimal separator.");
        }

        return coordinate;
    }

    private static ListingWriteResponse ToResponse(ListingWriteResult result) => new(
        result.ListingId, result.ListingId, result.Status, result.ImageUrls,
        result.Location?.Latitude, result.Location?.Longitude);

    private bool TryGetLandlordId(out Guid ownerId)
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out ownerId) && ownerId != Guid.Empty;
    }
}
