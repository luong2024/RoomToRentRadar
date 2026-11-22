using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Features.Auth;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Models.Moderation;
using RoomToRentRadar.Shared.Moderation;
using RoomToRentRadar.UseCases.Moderation;

namespace RoomToRentRadar.Api.Features.Moderation;

[ApiController]
[Authorize(Policy = AuthPolicies.ModeratorOrAdmin)]
[Route("api/v1/moderation/listings")]
public sealed class ModerationListingsController(
    IModerationQueueUseCase moderationQueueUseCase,
    IModerationActionsUseCase moderationActionsUseCase) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ModerationListingQueueDto), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<ModerationListingQueueDto>> GetQueue(
        [FromQuery] ModerationListingQueueParameters parameters,
        CancellationToken cancellationToken)
    {
        if (!ModerationListingQueueQueryValidator.TryCreate(parameters, out var query, out var problem))
        {
            return ToProblem(problem!);
        }

        ModerationListingQueueCursor? cursor = null;
        if (query.Cursor is not null)
        {
            if (!ModerationListingQueueCursorCodec.TryDecode(query.Cursor, out var decoded))
            {
                return ToProblem(ModerationProblemFactory.InvalidQuery(
                    ModerationProblemFactory.InvalidCursor,
                    "cursor is not a valid moderation listing queue cursor."));
            }

            if (decoded.Status != query.Status)
            {
                return ToProblem(ModerationProblemFactory.InvalidQuery(
                    ModerationProblemFactory.InvalidCursor,
                    "cursor does not match the requested status."));
            }

            cursor = decoded;
        }

        var queue = await moderationQueueUseCase.GetListingQueueAsync(
            query, cursor, cancellationToken);
        return Ok(queue);
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> ApproveListing(
        Guid id,
        CancellationToken cancellationToken)
    {
        var outcome = await moderationActionsUseCase.ApproveListingAsync(
            id, CurrentUserId, cancellationToken);
        return outcome switch
        {
            ModerationActionOutcome.Success => NoContent(),
            ModerationActionOutcome.ListingNotFound =>
                ToProblem(ModerationProblemFactory.ListingNotFoundProblem()),
            ModerationActionOutcome.InvalidListingTransition =>
                ToProblem(ModerationProblemFactory.InvalidApprovalTransitionProblem()),
            _ => ToProblem(ModerationProblemFactory.ApprovalConflictProblem())
        };
    }

    private Guid CurrentUserId
        => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private ObjectResult ToProblem(ProblemDetails problem)
        => Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail,
            type: problem.Type,
            extensions: problem.Extensions);
}
