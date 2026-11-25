using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Services.Moderation;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Controllers;

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
}
