using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Listings;

/// <summary>
/// Builds the RFC 9457 problem document for a drawn area that is well-formed JSON
/// but not a supported polygon (HTTP 422).
/// </summary>
public static class AreaSearchProblemFactory
{
    public static ProblemDetails InvalidArea(AreaValidationError error)
    {
        var problem = new ProblemDetails
        {
            // RFC 9110 section 15.5.21: 422 Unprocessable Content.
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.21",
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Invalid search area",
            Detail = error.Detail
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["field"] = error.Field;
        return problem;
    }
}
