using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Data.Configurations;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Api.Features.Auth;
using RoomToRentRadar.Api.Features.Moderation;
using RoomToRentRadar.Shared.Moderation;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.ModeratorOrAdmin)]
[Route("api/v1/moderation/reports")]
public sealed class ModerationReportsController(
    ModerationQueueService moderationQueueService,
    ModerationActionsService moderationActionsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ModerationReportQueueDto), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<ModerationReportQueueDto>> GetQueue(
        [FromQuery] ModerationQueueParameters parameters,
        CancellationToken cancellationToken)
    {
        if (!ModerationQueueQueryValidator.TryCreate(parameters, out var query, out var problem))
        {
            return ToProblem(problem!);
        }

        ModerationQueueCursor? cursor = null;
        if (query.Cursor is not null)
        {
            if (!ModerationQueueCursorCodec.TryDecode(query.Cursor, out var decoded))
            {
                return ToProblem(ModerationProblemFactory.InvalidQuery(
                    ModerationProblemFactory.InvalidCursor,
                    "cursor is not a valid moderation queue cursor."));
            }

            cursor = decoded;
        }

        var queue = await moderationQueueService.GetQueueAsync(query, cursor, cancellationToken);
        return Ok(queue);
    }

    [HttpPost("{id:guid}/claim")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> ClaimReport(
        Guid id,
        CancellationToken cancellationToken)
    {
        var outcome = await moderationActionsService.ClaimAsync(id, CurrentUserId, cancellationToken);
        return outcome switch
        {
            ModerationActionOutcome.Success => NoContent(),
            ModerationActionOutcome.ReportNotFound => ToProblem(ModerationProblemFactory.ReportNotFoundProblem()),
            _ => ToProblem(ModerationProblemFactory.ClaimConflictProblem(
                "The report group is already claimed by another moderator."))
        };
    }

    [HttpDelete("{id:guid}/claim")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> ReleaseReport(
        Guid id,
        CancellationToken cancellationToken)
    {
        var outcome = await moderationActionsService.ReleaseAsync(
            id, CurrentUserId, IsAdmin, cancellationToken);
        return outcome switch
        {
            ModerationActionOutcome.Success => NoContent(),
            ModerationActionOutcome.ReportNotFound => ToProblem(ModerationProblemFactory.ReportNotFoundProblem()),
            ModerationActionOutcome.NotClaimOwner => ToProblem(ModerationProblemFactory.NotClaimOwnerProblem()),
            _ => ToProblem(ModerationProblemFactory.ClaimConflictProblem(
                "The report group is not currently claimed or the claim has expired."))
        };
    }

    [HttpPost("{id:guid}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> ResolveReport(
        Guid id,
        [FromBody] ResolveModerationReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModerationResolutionActionWire.TryParse(request.Action?.Trim(), out var action))
        {
            return ToProblem(ModerationProblemFactory.InvalidResolutionActionProblem());
        }

        var note = request.Note?.Trim();
        if (string.IsNullOrEmpty(note))
        {
            return ToProblem(ModerationProblemFactory.InvalidNoteProblem("note is required."));
        }

        if (note.Length > ModerationAuditLogConfiguration.MaximumNoteLength)
        {
            return ToProblem(ModerationProblemFactory.InvalidNoteProblem(
                $"note must contain at most {ModerationAuditLogConfiguration.MaximumNoteLength} characters."));
        }

        var outcome = await moderationActionsService.ResolveAsync(
            id, CurrentUserId, IsAdmin, action, note, cancellationToken);
        return outcome switch
        {
            ModerationActionOutcome.Success => NoContent(),
            ModerationActionOutcome.ReportNotFound => ToProblem(ModerationProblemFactory.ReportNotFoundProblem()),
            ModerationActionOutcome.NotClaimOwner => ToProblem(ModerationProblemFactory.NotClaimOwnerProblem()),
            ModerationActionOutcome.ClaimConflict => ToProblem(ModerationProblemFactory.ClaimConflictProblem(
                "Claim the report group before resolving it.")),
            ModerationActionOutcome.InvalidReportState => ToProblem(ModerationProblemFactory.InvalidReportStateProblem(
                "The report group has no open or in-review reports.")),
            _ => ToProblem(ModerationProblemFactory.ResolutionConflictProblem())
        };
    }

    private Guid CurrentUserId
        => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private bool IsAdmin
        => User.IsInRole(UserRoleWire.Admin);

    private ObjectResult ToProblem(ProblemDetails problem)
        => Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail,
            type: problem.Type,
            extensions: problem.Extensions);
}
