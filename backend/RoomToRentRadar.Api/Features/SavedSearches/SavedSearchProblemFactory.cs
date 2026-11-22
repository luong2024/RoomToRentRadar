using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.SavedSearches;

public static class SavedSearchProblemFactory
{
    public const string InvalidName = "invalid_saved_search_name";
    public const string InvalidAlertState = "invalid_alert_state";
    public const string SavedSearchNotFound = "saved_search_not_found";

    public static ProblemDetails InvalidNameProblem(string detail)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid saved search",
            Detail = detail
        };
        problem.Extensions["code"] = InvalidName;
        problem.Extensions["field"] = "name";
        return problem;
    }

    public static ProblemDetails InvalidAlertStateProblem(string detail)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid saved search alert state",
            Detail = detail
        };
        problem.Extensions["code"] = InvalidAlertState;
        problem.Extensions["field"] = "alert_state";
        return problem;
    }

    public static ProblemDetails NotFoundProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            Status = StatusCodes.Status404NotFound,
            Title = "Saved search not found",
            Detail = "No saved search of the current user exists with the requested identifier."
        };
        problem.Extensions["code"] = SavedSearchNotFound;
        return problem;
    }
}
