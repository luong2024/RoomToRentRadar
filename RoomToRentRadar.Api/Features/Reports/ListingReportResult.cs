namespace RoomToRentRadar.Api.Features.Reports;

public enum ListingReportOutcome
{
    Created,
    ListingNotFound,
    OwnListing,
    DuplicateOpenReport,
    RateLimited
}

public sealed record ListingReportResult(
    ListingReportOutcome Outcome,
    Guid ReportId = default,
    TimeSpan RetryAfter = default)
{
    public static ListingReportResult Created(Guid reportId)
        => new(ListingReportOutcome.Created, reportId);

    public static ListingReportResult ListingNotFound()
        => new(ListingReportOutcome.ListingNotFound);

    public static ListingReportResult OwnListing()
        => new(ListingReportOutcome.OwnListing);

    public static ListingReportResult DuplicateOpenReport()
        => new(ListingReportOutcome.DuplicateOpenReport);

    public static ListingReportResult RateLimited(TimeSpan retryAfter)
        => new(ListingReportOutcome.RateLimited, RetryAfter: retryAfter);
}
