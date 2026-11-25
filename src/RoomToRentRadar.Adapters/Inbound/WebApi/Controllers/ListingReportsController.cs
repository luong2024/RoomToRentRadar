using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Adapters.Inbound.WebApi.Authorization;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Adapters.Inbound.WebApi.RateLimiting;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;

[ApiController]
[Produces("application/json")]
public sealed class ListingReportsController : ControllerBase
{
    private readonly ISubmitListingReportUseCase _submitUseCase;
    private readonly IGetModerationQueueUseCase _getQueueUseCase;
    private readonly IReviewListingReportUseCase _reviewUseCase;
    private readonly ILogger<ListingReportsController> _logger;

    public ListingReportsController(
        ISubmitListingReportUseCase submitUseCase,
        IGetModerationQueueUseCase getQueueUseCase,
        IReviewListingReportUseCase reviewUseCase,
        ILogger<ListingReportsController> logger)
    {
        _submitUseCase = submitUseCase ?? throw new ArgumentNullException(nameof(submitUseCase));
        _getQueueUseCase = getQueueUseCase ?? throw new ArgumentNullException(nameof(getQueueUseCase));
        _reviewUseCase = reviewUseCase ?? throw new ArgumentNullException(nameof(reviewUseCase));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("api/v1/rooms/{roomId}/reports")]
    [Authorize]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    [ProducesResponseType(typeof(ListingReportSubmittedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SubmitReport(
        [FromRoute] string roomId,
        [FromBody] SubmitListingReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(out var actorUserId))
        {
            return UnauthorizedProblem();
        }

        var result = await _submitUseCase.ExecuteAsync(
            new SubmitListingReportCommand(roomId, actorUserId, request.Reason, request.Note),
            cancellationToken);

        _logger.LogInformation(
            "Listing report {ReportId} submitted for room {RoomId} with status {Status}.",
            result.ReportId,
            result.RoomId,
            result.Status);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("api/v1/moderation/reports")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(PagedResult<ModerationReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetModerationQueue(
        [FromQuery] ReportStatus? status = ReportStatus.Pending,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _getQueueUseCase.ExecuteAsync(
            new GetModerationQueueQuery(status, page, pageSize),
            cancellationToken);

        return Ok(result);
    }

    [HttpPatch("api/v1/moderation/reports/{reportId}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ModerationReportItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReviewReport(
        [FromRoute] string reportId,
        [FromBody] ReviewListingReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(out var reviewerUserId))
        {
            return UnauthorizedProblem();
        }

        var result = await _reviewUseCase.ExecuteAsync(
            new ReviewListingReportCommand(
                reportId,
                request.RoomId,
                reviewerUserId,
                request.Status,
                request.ResolutionNote,
                request.ExpectedETag),
            cancellationToken);

        _logger.LogInformation(
            "Listing report {ReportId} for room {RoomId} transitioned to {Status}.",
            result.ReportId,
            result.RoomId,
            result.Status);

        return Ok(result);
    }

    private bool TryGetActorUserId(out Guid actorUserId)
    {
        actorUserId = Guid.Empty;
        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        return !string.IsNullOrWhiteSpace(subClaim)
            && Guid.TryParse(subClaim, out actorUserId)
            && actorUserId != Guid.Empty;
    }

    private UnauthorizedObjectResult UnauthorizedProblem() =>
        Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "A valid user identifier is missing from the security token.",
            Instance = HttpContext.Request.Path
        });
}
