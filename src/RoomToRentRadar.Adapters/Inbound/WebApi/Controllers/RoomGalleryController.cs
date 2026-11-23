using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Adapters.Inbound.WebApi.Authorization;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;

[ApiController]
[Route("api/v1/rooms/{roomId}/gallery")]
[Produces("application/json")]
public sealed class RoomGalleryController : ControllerBase
{
    public const long MaxMultipartRequestSizeBytes = 12 * 1024 * 1024; // 12 MB envelope for <= 10 MB image + form fields
    private const string GalleryCacheControlHeader = "public, max-age=300";

    private readonly IGetRoomGalleryUseCase _getRoomGalleryUseCase;
    private readonly IUploadRoomPhotoUseCase _uploadRoomPhotoUseCase;
    private readonly IDeleteRoomPhotoUseCase _deleteRoomPhotoUseCase;
    private readonly IReorderRoomPhotosUseCase _reorderRoomPhotosUseCase;
    private readonly IAssignRoomGalleryOwnerUseCase? _assignRoomGalleryOwnerUseCase;

    public RoomGalleryController(
        IGetRoomGalleryUseCase getRoomGalleryUseCase,
        IUploadRoomPhotoUseCase uploadRoomPhotoUseCase,
        IDeleteRoomPhotoUseCase deleteRoomPhotoUseCase,
        IReorderRoomPhotosUseCase reorderRoomPhotosUseCase,
        IAssignRoomGalleryOwnerUseCase? assignRoomGalleryOwnerUseCase = null)
    {
        _getRoomGalleryUseCase = getRoomGalleryUseCase ?? throw new ArgumentNullException(nameof(getRoomGalleryUseCase));
        _uploadRoomPhotoUseCase = uploadRoomPhotoUseCase ?? throw new ArgumentNullException(nameof(uploadRoomPhotoUseCase));
        _deleteRoomPhotoUseCase = deleteRoomPhotoUseCase ?? throw new ArgumentNullException(nameof(deleteRoomPhotoUseCase));
        _reorderRoomPhotosUseCase = reorderRoomPhotosUseCase ?? throw new ArgumentNullException(nameof(reorderRoomPhotosUseCase));
        _assignRoomGalleryOwnerUseCase = assignRoomGalleryOwnerUseCase;
    }

    /// <summary>
    /// Retrieves all photos (standard and 360°) for a room gallery (US03 / T03.1).
    /// Supports HTTP Cache-Control and ETag / 304 Not Modified for progressive client caching (AC3).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RoomGalleryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGallery(
        [FromRoute] string roomId,
        CancellationToken cancellationToken)
    {
        var gallery = await _getRoomGalleryUseCase.ExecuteAsync(roomId, cancellationToken);
        var etag = ComputeGalleryETag(gallery);

        if (Response is not null)
        {
            Response.Headers.CacheControl = GalleryCacheControlHeader;
            Response.Headers.ETag = etag;
        }

        if (Request?.Headers.TryGetValue("If-None-Match", out var ifNoneMatch) == true &&
            string.Equals(ifNoneMatch.ToString().Trim(), etag, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Ok(gallery);
    }

    /// <summary>
    /// Uploads, validates, compresses, and stores a room photo (standard or 360° panorama).
    /// </summary>
    [HttpPost("photos")]
    [Authorize(Roles = $"{AuthorizationPolicies.Roles.Landlord},{AuthorizationPolicies.Roles.Admin}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxMultipartRequestSizeBytes)]
    [ProducesResponseType(typeof(RoomPhotoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UploadPhoto(
        [FromRoute] string roomId,
        [FromForm] UploadRoomPhotoFormRequest form,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var authError))
        {
            return authError!;
        }

        if (form?.File is null || form.File.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Photo Upload",
                Detail = "A non-empty image file is required."
            });
        }

        if (form.File.Length > RoomPhoto.MaxFileSizeBytes)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Photo Upload",
                Detail = $"Photo file exceeds the maximum allowed size of {RoomPhoto.MaxFileSizeBytes / (1024 * 1024)} MB."
            });
        }

        byte[] fileBytes;
        await using (var ms = new MemoryStream((int)form.File.Length))
        {
            await form.File.CopyToAsync(ms, cancellationToken);
            fileBytes = ms.ToArray();
        }

        var command = new UploadRoomPhotoCommand(
            RoomId: roomId,
            UploadedByUserId: userId,
            FileName: form.File.FileName ?? "photo.jpg",
            ContentType: form.File.ContentType ?? string.Empty,
            FileBytes: fileBytes,
            Is360: form.Is360,
            IsCover: form.IsCover,
            Caption: form.Caption,
            IsAdmin: IsCurrentUserAdmin());

        var created = await _uploadRoomPhotoUseCase.ExecuteAsync(command, cancellationToken);
        return Created($"/api/v1/rooms/{Uri.EscapeDataString(created.RoomId)}/gallery", created);
    }

    /// <summary>
    /// Deletes a photo from a room gallery and removes its stored objects.
    /// </summary>
    [HttpDelete("photos/{photoId}")]
    [Authorize(Roles = $"{AuthorizationPolicies.Roles.Landlord},{AuthorizationPolicies.Roles.Admin}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePhoto(
        [FromRoute] string roomId,
        [FromRoute] string photoId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var authError))
        {
            return authError!;
        }

        await _deleteRoomPhotoUseCase.ExecuteAsync(roomId, photoId, userId, IsCurrentUserAdmin(), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Reorders gallery photos and/or updates the cover photo.
    /// </summary>
    [HttpPatch("reorder")]
    [Authorize(Roles = $"{AuthorizationPolicies.Roles.Landlord},{AuthorizationPolicies.Roles.Admin}")]
    [ProducesResponseType(typeof(RoomGalleryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReorderPhotos(
        [FromRoute] string roomId,
        [FromBody] ReorderRoomPhotosRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var authError))
        {
            return authError!;
        }

        var command = new ReorderRoomPhotosCommand(
            RoomId: roomId,
            ActorUserId: userId,
            OrderedPhotoIds: request?.OrderedPhotoIds ?? [],
            CoverPhotoId: request?.CoverPhotoId,
            IsAdmin: IsCurrentUserAdmin());

        var updated = await _reorderRoomPhotosUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Admin-only backfill endpoint to assign a verified Landlord owner to an existing room lacking LandlordId.
    /// </summary>
    [HttpPatch("owner")]
    [Authorize(Roles = AuthorizationPolicies.Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignOwner(
        [FromRoute] string roomId,
        [FromBody] AssignRoomOwnerRequest? request,
        CancellationToken cancellationToken)
    {
        if (_assignRoomGalleryOwnerUseCase is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        await _assignRoomGalleryOwnerUseCase.ExecuteAsync(
            roomId,
            request?.LandlordId ?? Guid.Empty,
            cancellationToken);
        return NoContent();
    }

    private bool IsCurrentUserAdmin() =>
        User?.IsInRole(AuthorizationPolicies.Roles.Admin) == true;

    private bool TryGetCurrentUserId(out Guid userId, out IActionResult? errorResult)
    {
        var userIdClaim = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");
        if (Guid.TryParse(userIdClaim, out userId) && userId != Guid.Empty)
        {
            errorResult = null;
            return true;
        }

        userId = Guid.Empty;
        errorResult = Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "A valid authenticated user identity is required."
        });
        return false;
    }

    private static string ComputeGalleryETag(RoomGalleryResponse gallery)
    {
        var sb = new StringBuilder();
        sb.Append(gallery.RoomId).Append('|').Append(gallery.TotalCount);
        foreach (var photo in gallery.Photos)
        {
            sb.Append('|')
              .Append(photo.Id).Append(':')
              .Append(photo.DisplayOrder).Append(':')
              .Append(photo.IsCover ? '1' : '0').Append(':')
              .Append(photo.Is360 ? '1' : '0').Append(':')
              .Append(photo.UploadedAtUtc.ToUnixTimeMilliseconds());
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return $"\"{Convert.ToHexString(hash[..12]).ToLowerInvariant()}\"";
    }
}
