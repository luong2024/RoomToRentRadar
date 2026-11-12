using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Authorization;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Requests.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/landlord/photos")]
public sealed class PhotosController : ControllerBase
{
    private readonly IUploadPhotoUseCase _uploadPhotoUseCase;
    private readonly IDeletePhotoUseCase _deletePhotoUseCase;

    public PhotosController(
        IUploadPhotoUseCase uploadPhotoUseCase,
        IDeletePhotoUseCase deletePhotoUseCase)
    {
        ArgumentNullException.ThrowIfNull(uploadPhotoUseCase);
        ArgumentNullException.ThrowIfNull(deletePhotoUseCase);

        _uploadPhotoUseCase = uploadPhotoUseCase;
        _deletePhotoUseCase = deletePhotoUseCase;
    }

    // uploadPhoto — UC-30, BR-MED-01..03.
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.VerifiedLandlordPolicy)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    [ProducesResponseType(typeof(PhotoUploadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<PhotoUploadDto>> Upload(
        [FromForm] IFormFile? file,
        [FromForm] string? kind,
        CancellationToken ct)
    {
        // RoomId/UploadedByUserId are never accepted from the client (whitelist).
        if (file is null)
        {
            throw new DomainValidationException("Photo file is required.");
        }

        // Bind kind as a string and whitelist it here (GUARDRAILS §1.5.1). Letting [ApiController]
        // enum-bind it short-circuits with a ValidationProblemDetails that carries no errorCode and
        // never reaches the middleware. The multipart wire format is unchanged (enum name string).
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new DomainValidationException("Photo kind is required.");
        }

        if (!Enum.TryParse<PhotoKind>(kind.Trim(), ignoreCase: true, out var photoKind) || !Enum.IsDefined(photoKind))
        {
            throw new DomainValidationException("Photo kind is invalid.");
        }

        var userId = User.GetRequiredUserId();

        // Stream straight from the request; never buffer the upload into memory.
        await using var stream = file.OpenReadStream();

        var dto = await _uploadPhotoUseCase.ExecuteAsync(
            new UploadPhotoRequest(userId, photoKind, file.FileName, stream, file.Length),
            ct);

        return Created(dto.MediumUrl, dto);
    }

    // deletePhoto — BR-MED-03.
    [HttpDelete("{photoId}")]
    [Authorize(Policy = AuthorizationPolicies.VerifiedLandlordPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete([FromRoute] string photoId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var result = await _deletePhotoUseCase.ExecuteAsync(photoId, userId, ct);

        return result switch
        {
            DeletePhotoResult.Deleted => NoContent(),
            DeletePhotoResult.Attached => PhotoProblem(
                StatusCodes.Status409Conflict,
                "Conflict",
                "PHOTO_ATTACHED",
                "Photo is attached to a room and cannot be deleted."),
            // Anti-IDOR: a missing photo and another user's photo stay indistinguishable.
            _ => PhotoProblem(
                StatusCodes.Status404NotFound,
                "Not Found",
                "PHOTO_NOT_FOUND",
                "Photo was not found.")
        };
    }

    // Two branches do not justify a new class/service (KISS): a small local ProblemDetails builder
    // mirroring the middleware shape, with the errorCode extension member and problem+json content type.
    private ObjectResult PhotoProblem(int statusCode, string title, string errorCode, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["errorCode"] = errorCode;

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }
}
