using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Api.Features.Reports;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/listings/{listingId:guid}/reports")]
public sealed class ListingReportsController(ListingReportsService listingReportsService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CreateListingReportResponse), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<CreateListingReportResponse>> CreateReport(
        Guid listingId,
        [FromBody] CreateListingReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!ListingReportReasonWire.TryParse(request.Reason?.Trim(), out var reason))
        {
            return ToProblem(ReportProblemFactory.InvalidReasonProblem());
        }

        if (request.Details is { Length: > ReportDetailsSanitizer.MaximumInputLength })
        {
            return ToProblem(ReportProblemFactory.InvalidDetailsProblem(
                $"details must contain at most {ReportDetailsSanitizer.MaximumInputLength} characters "
                + "before HTML and control characters are removed."));
        }

        var details = ReportDetailsSanitizer.Sanitize(request.Details);
        if (details is { Length: > ReportDetailsSanitizer.MaximumLength })
        {
            return ToProblem(ReportProblemFactory.InvalidDetailsProblem(
                $"details must contain at most {ReportDetailsSanitizer.MaximumLength} characters "
                + "after HTML and control characters are removed."));
        }

        var result = await listingReportsService.CreateAsync(
            CurrentUserId,
            listingId,
            reason,
            details,
            cancellationToken);

        return result.Outcome switch
        {
            ListingReportOutcome.Created =>
                Created(
                    $"/api/v1/moderation/reports/{result.ReportId}",
                    new CreateListingReportResponse(result.ReportId)),
            ListingReportOutcome.ListingNotFound =>
                ToProblem(ListingProblemFactory.NotFound()),
            ListingReportOutcome.OwnListing =>
                ToProblem(ReportProblemFactory.CannotReportOwnListingProblem()),
            ListingReportOutcome.DuplicateOpenReport =>
                ToProblem(ReportProblemFactory.DuplicateOpenReportProblem()),
            _ => RateLimited(result.RetryAfter)
        };
    }

    private Guid CurrentUserId
        => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private ObjectResult RateLimited(TimeSpan retryAfter)
    {
        var problem = ReportProblemFactory.RateLimitedProblem(retryAfter);
        Response.Headers.RetryAfter =
            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return ToProblem(problem);
    }

    private ObjectResult ToProblem(ProblemDetails problem)
        => Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail,
            type: problem.Type,
            extensions: problem.Extensions);
}
