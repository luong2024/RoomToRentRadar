using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Moderation;

public static class ModerationProblemFactory
{
    public const string InvalidLimit = "invalid_limit";
    public const string InvalidCursor = "invalid_cursor";
    public const string InvalidResolutionAction = "invalid_resolution_action";
    public const string InvalidModerationNote = "invalid_moderation_note";
    public const string ReportNotFound = "report_not_found";
    public const string ClaimConflict = "claim_conflict";
    public const string NotClaimOwner = "not_claim_owner";
    public const string InvalidReportState = "invalid_report_state";
    public const string InvalidListingTransition = "invalid_listing_transition";
    public const string ResolutionConflict = "resolution_conflict";

    public static ProblemDetails InvalidQuery(string code, string detail)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid moderation queue query",
            Detail = detail
        };
        problem.Extensions["code"] = code;
        return problem;
    }

    public static ProblemDetails InvalidResolutionActionProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid resolution action",
            Detail = "action must be one of: dismiss, hide_listing, remove_listing, warn_owner."
        };
        problem.Extensions["code"] = InvalidResolutionAction;
        problem.Extensions["field"] = "action";
        return problem;
    }

    public static ProblemDetails InvalidNoteProblem(string detail)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid moderation note",
            Detail = detail
        };
        problem.Extensions["code"] = InvalidModerationNote;
        problem.Extensions["field"] = "note";
        return problem;
    }

    public static ProblemDetails ReportNotFoundProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            Status = StatusCodes.Status404NotFound,
            Title = "Report not found",
            Detail = "No active report exists with the requested identifier."
        };
        problem.Extensions["code"] = ReportNotFound;
        return problem;
    }

    public static ProblemDetails ClaimConflictProblem(string detail)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            Status = StatusCodes.Status409Conflict,
            Title = "Claim conflict",
            Detail = detail
        };
        problem.Extensions["code"] = ClaimConflict;
        return problem;
    }

    public static ProblemDetails NotClaimOwnerProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            Status = StatusCodes.Status403Forbidden,
            Title = "Not the claim owner",
            Detail = "Only the moderator holding the claim or an admin can act on this report group."
        };
        problem.Extensions["code"] = NotClaimOwner;
        return problem;
    }

    public static ProblemDetails InvalidReportStateProblem(string detail)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            Status = StatusCodes.Status409Conflict,
            Title = "Invalid report state",
            Detail = detail
        };
        problem.Extensions["code"] = InvalidReportState;
        return problem;
    }

    public static ProblemDetails InvalidListingTransitionProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            Status = StatusCodes.Status409Conflict,
            Title = "Invalid listing transition",
            Detail = "A removed listing cannot be hidden; resolve the group with dismiss, "
                + "remove_listing or warn_owner instead."
        };
        problem.Extensions["code"] = InvalidListingTransition;
        return problem;
    }

    public static ProblemDetails ResolutionConflictProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            Status = StatusCodes.Status409Conflict,
            Title = "Resolution conflict",
            Detail = "The report group or its listing changed concurrently; refresh the queue and try again."
        };
        problem.Extensions["code"] = ResolutionConflict;
        return problem;
    }
}
