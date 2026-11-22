using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Travel;

public static class TravelProblemFactory
{
    public const string InvalidDestination = "invalid_travel_destination";

    public const string RateLimited = "travel_rate_limited";

    public static ProblemDetails InvalidDestinationProblem(string detail)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid travel destination",
            Detail = detail
        };
        problem.Extensions["code"] = InvalidDestination;
        return problem;
    }

    public static ProblemDetails RateLimitedProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc6585#section-4",
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many travel requests",
            Detail = "Travel estimate requests are limited per client. Retry after a short delay."
        };
        problem.Extensions["code"] = RateLimited;
        return problem;
    }
}
