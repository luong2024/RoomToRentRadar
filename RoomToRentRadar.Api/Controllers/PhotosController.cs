using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Authorization;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;

namespace RoomToRentRadar.Api.Controllers;

/// <summary>
/// Landlord photo upload/delete (<c>operationId: uploadPhoto</c> / <c>deletePhoto</c>, US08 AC4).
/// Both actions require a verified landlord (BR-AUTH-03); the use cases enforce ownership (BR-MED-03).
/// </summary>
[ApiController]
[Route("api/landlord/photos")]
[Authorize(Policy = AuthorizationPolicies.VerifiedLandlordPolicy)]
public sealed class PhotosController : ControllerBase
{
    /// <summary>
    /// 11 MB of transport headroom so a file slightly above 10 MB reaches the use case and yields
    /// <c>413 FILE_TOO_LARGE</c> instead of a bare Kestrel <c>BadHttpRequestException</c>.
    /// </summary>
    private const long RequestSizeLimitBytes = 11L * 1024 * 1024;

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

    [HttpPost]
    [RequestSizeLimit(RequestSizeLimitBytes)]
    [ProducesResponseType(typeof(PhotoUploadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<PhotoUploadDto>> UploadPhoto(
        [FromForm] IFormFile file,
        [FromForm] PhotoKind? kind,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();

        // BR-MED-02 / openapi uploadPhoto: `kind` is required; a missing form field must be a
        // VALIDATION_FAILED 400, never a silent default to Standard. (An unknown enum name is already
        // rejected by model binding with the same 400 text through ValidationProblemResponseFactory.)
        if (kind is null)
        {
            throw new DomainValidationException(
                "The 'kind' form field is required (Standard or Panorama).",
                ProblemCodes.ValidationFailed);
        }

        await using var content = file.OpenReadStream();
        var request = new UploadPhotoRequest(
            UserId: userId,
            Kind: kind.Value,
            FileName: file.FileName,
            Content: content,
            Length: file.Length);

        var result = await _uploadPhotoUseCase.ExecuteAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("{photoId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeletePhoto(
        [FromRoute] string photoId,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _deletePhotoUseCase.ExecuteAsync(userId, photoId, ct);
        return NoContent();
    }
}
