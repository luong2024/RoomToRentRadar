using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Models.Common;
using RoomToRentRadar.Ports.Models.Photos;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Photos;

[ApiController]
[Route("api/v1/listings/{listingId:guid}/photos")]
public sealed class ListingPhotosController(IListingPhotosUseCase listingPhotosUseCase) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ListingPhotoDto>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ListingPhotoDto>>> GetPhotos(
        Guid listingId,
        CancellationToken cancellationToken)
    {
        var result = await listingPhotosUseCase.GetGalleryAsync(
            listingId,
            CurrentUserIdOrNull,
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error!);
    }

    [Authorize]
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PhotoUploadLimits.MaxRequestBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = PhotoUploadLimits.MaxRequestBodyBytes)]
    [ProducesResponseType(typeof(IReadOnlyList<ListingPhotoDto>), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
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

        var uploads = files!
            .Select(file => new PhotoUpload(
                file.FileName,
                file.ContentType,
                file.Length,
                file.OpenReadStream()))
            .ToList();

        UseCaseResult<IReadOnlyList<ListingPhotoDto>> result;
        try
        {
            result = await listingPhotosUseCase.UploadAsync(
                listingId,
                CurrentUserId,
                photoKind,
                uploads,
                cancellationToken);
        }
        finally
        {
            foreach (var upload in uploads)
            {
                upload.Content.Dispose();
            }
        }

        return result.IsSuccess
            ? Created($"/api/v1/listings/{listingId}/photos", result.Value)
            : ToProblem(result.Error!);
    }

    private Guid? CurrentUserIdOrNull
        => User.Identity?.IsAuthenticated == true
            && Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? userId
                : null;

    private Guid CurrentUserId
        => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private ObjectResult ToProblem(ApplicationError error)
        => ToProblem(PhotoProblemFactory.FromError(error));

    private ObjectResult ToProblem(ProblemDetails problem)
        => Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail,
            type: problem.Type,
            extensions: problem.Extensions);
}
