using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Features.Photos;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/v1/listings/{listingId:guid}/photos")]
public sealed class ListingPhotosController(ListingPhotosService listingPhotosService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ListingPhotoDto>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ListingPhotoDto>>> GetPhotos(
        Guid listingId,
        CancellationToken cancellationToken)
    {
        var result = await listingPhotosService.GetGalleryAsync(listingId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Problem!);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PhotoUploadLimits.MaxRequestBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = PhotoUploadLimits.MaxRequestBodyBytes)]
    [ProducesResponseType(typeof(IReadOnlyList<ListingPhotoDto>), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ListingPhotoDto>>> UploadPhotos(
        Guid listingId,
        [FromForm] IFormFileCollection? files,
        [FromForm] string? kind,
        CancellationToken cancellationToken)
    {
        if (!PhotoUploadValidator.TryValidate(files, kind, out var photoKind, out var validationProblem))
        {
            return ToProblem(validationProblem!);
        }

        var result = await listingPhotosService.UploadAsync(listingId, photoKind, files!, cancellationToken);
        return result.IsSuccess
            ? Created($"/api/v1/listings/{listingId}/photos", result.Value)
            : ToProblem(result.Problem!);
    }

    private ObjectResult ToProblem(ProblemDetails problem)
        => Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail,
            type: problem.Type,
            extensions: problem.Extensions);
}
