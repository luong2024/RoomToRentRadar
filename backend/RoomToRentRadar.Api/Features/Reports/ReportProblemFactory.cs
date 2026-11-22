using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Reports;

public static class ReportProblemFactory
{
    public const string InvalidReason = "invalid_report_reason";
    public const string InvalidDetails = "invalid_report_details";
    public const string CannotReportOwnListing = "cannot_report_own_listing";
    public const string DuplicateOpenReport = "duplicate_open_report";
    public const string RateLimited = "report_rate_limited";

    public static ProblemDetails InvalidReasonProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid report reason",
            Detail = "reason must be one of: spam, wrong_info, scam, offensive, already_rented."
        };
        problem.Extensions["code"] = InvalidReason;
        problem.Extensions["field"] = "reason";
        return problem;
    }

    public static ProblemDetails InvalidDetailsProblem(string detail)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid report details",
            Detail = detail
        };
        problem.Extensions["code"] = InvalidDetails;
        problem.Extensions["field"] = "details";
        return problem;
    }

    public static ProblemDetails CannotReportOwnListingProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.21",
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Cannot report own listing",
            Detail = "Users cannot report a listing they own."
        };
        problem.Extensions["code"] = CannotReportOwnListing;
        return problem;
    }

    public static ProblemDetails DuplicateOpenReportProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            Status = StatusCodes.Status409Conflict,
            Title = "Duplicate open report",
            Detail = "The current user already has an open report for this listing."
        };
        problem.Extensions["code"] = DuplicateOpenReport;
        return problem;
    }

    public static ProblemDetails RateLimitedProblem(TimeSpan retryAfter)
    {
        var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc6585#section-4",
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Report rate limit reached",
            Detail = "At most 5 reports are accepted per user per hour."
        };
        problem.Extensions["code"] = RateLimited;
        problem.Extensions["retry_after_seconds"] = retryAfterSeconds;
        return problem;
    }
}
