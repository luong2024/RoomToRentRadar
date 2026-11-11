using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Authorization;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Requests.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/rooms/{roomId}/photos")]
public sealed class PhotosController : ControllerBase
{
    private readonly IUploadRoomPhotosUseCase _uploadRoomPhotosUseCase;
    private readonly IGetRoomGalleryUseCase _getRoomGalleryUseCase;
    private readonly IDeleteRoomPhotoUseCase _deleteRoomPhotoUseCase;

    public PhotosController(
        IUploadRoomPhotosUseCase uploadRoomPhotosUseCase,
        IGetRoomGalleryUseCase getRoomGalleryUseCase,
        IDeleteRoomPhotoUseCase deleteRoomPhotoUseCase)
    {
        ArgumentNullException.ThrowIfNull(uploadRoomPhotosUseCase);
        ArgumentNullException.ThrowIfNull(getRoomGalleryUseCase);
        ArgumentNullException.ThrowIfNull(deleteRoomPhotoUseCase);

        _uploadRoomPhotosUseCase = uploadRoomPhotosUseCase;
        _getRoomGalleryUseCase = getRoomGalleryUseCase;
        _deleteRoomPhotoUseCase = deleteRoomPhotoUseCase;
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.VerifiedLandlordPolicy)]
    [RequestSizeLimit(110 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 110 * 1024 * 1024)]
    [ProducesResponseType(typeof(IReadOnlyList<RoomPhotoDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<RoomPhotoDto>>> Upload(
        [FromRoute] string roomId,
        [FromForm] List<IFormFile> files,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();

        if (files is null || files.Count == 0)
        {
            throw new DomainValidationException("At least one photo file is required.");
        }

        if (files.Count > PhotoFileRules.MaxPhotosPerRoom)
        {
            throw new DomainValidationException($"A room can have at most {PhotoFileRules.MaxPhotosPerRoom} photos.");
        }

        var streams = new List<MemoryStream>(files.Count);

        try
        {
            var inputs = new List<PhotoFileInput>(files.Count);
            foreach (var file in files)
            {
                if (file.Length > PhotoFileRules.MaxFileBytes)
                {
                    throw new DomainValidationException("Each photo must be 10 MB or smaller.");
                }

                var buffer = new MemoryStream((int)file.Length);
                streams.Add(buffer);

                await file.CopyToAsync(buffer, ct);
                buffer.Position = 0;

                inputs.Add(new PhotoFileInput(file.FileName, buffer, file.Length));
            }

            var dtos = await _uploadRoomPhotosUseCase.ExecuteAsync(
                new UploadRoomPhotosRequest(roomId, userId, inputs),
                ct);

            return StatusCode(StatusCodes.Status201Created, dtos);
        }
        finally
        {
            foreach (var stream in streams)
            {
                stream.Dispose();
            }
        }
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RoomGalleryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoomGalleryDto>> GetGallery(
        [FromRoute] string roomId,
        CancellationToken ct)
    {
        var gallery = await _getRoomGalleryUseCase.ExecuteAsync(roomId, ct);
        return Ok(gallery);
    }

    [HttpDelete("{photoId}")]
    [Authorize(Policy = AuthorizationPolicies.VerifiedLandlordPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute] string roomId,
        [FromRoute] string photoId,
        CancellationToken ct)
    {
        var deleted = await _deleteRoomPhotoUseCase.ExecuteAsync(roomId, photoId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
