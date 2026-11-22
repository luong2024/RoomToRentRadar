using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Shared.Notifications;

namespace RoomToRentRadar.Api.Features.Notifications;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public sealed class NotificationsController(INotificationsUseCase notificationsUseCase) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationDto>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetNotifications(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 50)
        {
            return ToProblem(NotificationProblemFactory.InvalidLimitProblem());
        }

        var notifications = await notificationsUseCase.GetRecentAsync(
            CurrentUserId,
            limit,
            cancellationToken);
        return Ok(notifications);
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
