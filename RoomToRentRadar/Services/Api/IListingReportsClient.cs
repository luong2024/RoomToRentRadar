using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.Services.Api;

public enum ListingReportSubmitOutcome
{
    Created,
    Unauthorized,
    ListingNotFound,
    OwnListing,
    DuplicateOpenReport,
    RateLimited
}

public sealed record ListingReportSubmitResult(
    ListingReportSubmitOutcome Outcome,
    Guid? ReportId = null,
    TimeSpan RetryAfter = default);

public interface IListingReportsClient
{
    Task<ListingReportSubmitResult> CreateReportAsync(
        Guid listingId,
        ListingReportReason reason,
        string? details,
        CancellationToken cancellationToken = default);
}
