using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Notifications;

public static class NotificationProblemFactory
{
    public const string InvalidLimit = "invalid_limit";

    public static ProblemDetails InvalidLimitProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid notifications query",
            Detail = "limit must be between 1 and 50."
        };
        problem.Extensions["code"] = InvalidLimit;
        return problem;
    }
}
