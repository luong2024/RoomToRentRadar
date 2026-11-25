using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Services.Moderation;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/v1/moderation")]
[Produces("application/json")]
public sealed class ModerationController(IListingModerationService moderationService) : ControllerBase
{
    /// <summary>Lấy hàng đợi kiểm duyệt tin đăng (US08 T08.3).</summary>
    [HttpGet("listings")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ModerationQueueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ModerationQueueResponse>> GetQueue(
        [FromQuery] string? moderationStatus = null,
        [FromQuery] string? campusId = null,
        CancellationToken cancellationToken = default)
    {
        if (!ControllerIdentityHelper.TryResolveModerator(this, out _, out var authError))
        {
            return authError!;
        }

        try
        {
            return Ok(await moderationService.GetModerationQueueAsync(moderationStatus, campusId, cancellationToken));
        }
        catch (ListingManagementValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Tham số lọc không hợp lệ");
        }
    }

    /// <summary>Duyệt (Approve -> Available + IsVerified=true) hoặc từ chối (Reject + Note) tin đăng.</summary>
    [HttpPut("listings/{listingId}/status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ModerationDecisionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModerationDecisionResponse>> Decide(
        string listingId,
        [FromBody] ModerationDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ControllerIdentityHelper.TryResolveModerator(this, out var moderatorId, out var authError))
        {
            return authError!;
        }

        try
        {
            return Ok(await moderationService.ModerateListingAsync(listingId, moderatorId, request, cancellationToken));
        }
        catch (ListingManagementValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Quyết định kiểm duyệt không hợp lệ");
        }
        catch (ListingManagementNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Không tìm thấy phòng");
        }
    }

    /// <summary>Lấy lịch sử kiểm duyệt (audit logs) của một tin đăng.</summary>
    [HttpGet("listings/{listingId}/history")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<ModerationAuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ModerationAuditLogDto>>> GetHistory(
        string listingId,
        CancellationToken cancellationToken = default)
    {
        if (!ControllerIdentityHelper.TryResolveModerator(this, out _, out var authError))
        {
            return authError!;
        }

        try
        {
            return Ok(await moderationService.GetModerationHistoryAsync(listingId, cancellationToken));
        }
        catch (ListingManagementValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Yêu cầu không hợp lệ");
        }
        catch (ListingManagementNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Không tìm thấy phòng");
        }
    }
}
