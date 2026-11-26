using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using RoomToRentRadar.Api.Services;
using RoomToRentRadar.Api.Services.Landlord;
using RoomToRentRadar.Api.Services.Moderation;
using RoomToRentRadar.Api.Services.Photos;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Controllers;

/// <summary>
/// US08 — khu vực chủ trọ. T08.2: đăng tin + upload ảnh (POST), xem chi tiết tin của mình (GET {id}).
/// T08.3: danh sách tin, đổi trạng thái, xóa mềm.
/// </summary>
[ApiController]
[Route("api/v1/landlord/listings")]
[Produces("application/json")]
public sealed class LandlordListingsController(IListingModerationService moderationService) : ControllerBase
{
    /// <summary>Lấy danh sách tất cả tin đăng của chủ trọ hiện tại (US08 T08.3).</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LandlordListingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LandlordListingsResponse>> GetMyListings(
        [FromQuery] string? status = null,
        [FromQuery] string? moderationStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (!ControllerIdentityHelper.TryResolveUserId(this, out var landlordId, out var authError))
        {
            return authError!;
        }

        return Ok(await moderationService.GetLandlordListingsAsync(
            landlordId, status, moderationStatus, cancellationToken));
    }

    /// <summary>Chủ trọ chuyển trạng thái phòng của mình (Available | Rented | Hidden).</summary>
    [HttpPatch("{listingId}/availability")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UpdateListingAvailabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UpdateListingAvailabilityResponse>> UpdateAvailability(
        string listingId,
        [FromBody] UpdateListingAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ControllerIdentityHelper.TryResolveUserId(this, out var landlordId, out var authError))
        {
            return authError!;
        }

        try
        {
            return Ok(await moderationService.UpdateLandlordAvailabilityAsync(
                listingId, landlordId, request, cancellationToken));
        }
        catch (ListingManagementValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Cập nhật trạng thái không hợp lệ");
        }
        catch (ListingManagementForbiddenException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Không có quyền thao tác");
        }
        catch (ListingManagementNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Không tìm thấy phòng");
        }
    }

    /// <summary>Chủ trọ xóa/ẩn mềm tin đăng của mình (Archived).</summary>
    [HttpDelete("{listingId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ArchiveListingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArchiveListingResponse>> ArchiveListing(
        string listingId,
        CancellationToken cancellationToken = default)
    {
        if (!ControllerIdentityHelper.TryResolveUserId(this, out var landlordId, out var authError))
        {
            return authError!;
        }

        try
        {
            return Ok(await moderationService.ArchiveLandlordListingAsync(
                listingId, landlordId, cancellationToken));
        }
        catch (ListingManagementValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Yêu cầu không hợp lệ");
        }
        catch (ListingManagementForbiddenException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Không có quyền thao tác");
        }
        catch (ListingManagementNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Không tìm thấy phòng");
        }
    }

    private const long MaxCreateRequestBytes = 110L * 1024 * 1024; // 10 ảnh x 10 MB + field text

    /// <summary>
    /// US08 T08.2 — chủ trọ đăng tin mới (multipart/form-data): các field của <see cref="CreateListingRequestDto"/>
    /// + file ảnh ở field <c>photos</c> (hoặc <c>files</c> như T03.1), &gt;= 3 ảnh JPG/PNG &lt;= 10MB,
    /// + <c>is360</c> (lặp lại, cùng thứ tự, thiếu = false). Tin mới luôn PendingReview, isVerified = false.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxCreateRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxCreateRequestBytes)]
    [ProducesResponseType(typeof(LandlordListingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LandlordListingDto>> CreateListing(
        [FromForm, ValidateNever] CreateListingRequestDto request, // validate bằng ListingCreationRules (thông báo tiếng Việt, dùng chung MAUI)
        [FromForm] List<IFormFile>? photos,
        [FromForm] List<IFormFile>? files,
        [FromForm] List<bool>? is360,
        [FromServices] ILandlordListingService landlordListingService,
        CancellationToken cancellationToken = default)
    {
        if (!ControllerIdentityHelper.TryResolveUserId(this, out var landlordId, out var authError))
        {
            return authError!;
        }

        // RequestValidationException → 400 do RequestValidationExceptionFilter xử lý.
        var items = ToUploadItems([.. photos ?? [], .. files ?? []], is360 ?? []);
        try
        {
            var created = await landlordListingService.CreateAsync(landlordId, request, items, cancellationToken);
            return CreatedAtAction(nameof(GetMyListing), new { listingId = created.Id }, created);
        }
        finally
        {
            foreach (var item in items)
            {
                await item.Content.DisposeAsync();
            }
        }
    }

    /// <summary>Ghép file với cờ <c>is360</c> cùng vị trí (thiếu cờ = false).</summary>
    private static List<PhotoUploadItem> ToUploadItems(IReadOnlyList<IFormFile> uploads, IReadOnlyList<bool> is360Flags) =>
        uploads
            .Select((file, i) => new PhotoUploadItem(
                file.FileName,
                file.Length,
                file.OpenReadStream(),
                i < is360Flags.Count && is360Flags[i]))
            .ToList();

    /// <summary>US08 T08.2 — chi tiết một tin của chủ trọ (mọi trạng thái, kèm ảnh); tin của người khác =&gt; 404.</summary>
    [HttpGet("{listingId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LandlordListingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LandlordListingDto>> GetMyListing(
        string listingId,
        [FromServices] ILandlordListingService landlordListingService,
        CancellationToken cancellationToken = default)
    {
        if (!ControllerIdentityHelper.TryResolveUserId(this, out var landlordId, out var authError))
        {
            return authError!;
        }

        var listing = await landlordListingService.GetAsync(landlordId, listingId, cancellationToken);
        return listing is null
            ? Problem(detail: $"Không tìm thấy tin đăng '{listingId}'.", statusCode: StatusCodes.Status404NotFound, title: "Không tìm thấy phòng")
            : Ok(listing);
    }
}
