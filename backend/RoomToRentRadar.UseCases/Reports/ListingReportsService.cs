using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Models.Reports;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.UseCases.Reports;

public sealed class ListingReportsService(
    IListingLookupRepository listingRepository,
    IListingReportRepository reportRepository) : IListingReportsUseCase
{
    public const int MaximumReportsPerUserPerHour = 5;

    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromHours(1);

    public async Task<ListingReportResult> CreateAsync(
        Guid reporterId,
        Guid listingId,
        ListingReportReason reason,
        string? details,
        CancellationToken cancellationToken)
    {
        var target = await listingRepository.FindPublishedReportTargetAsync(
            listingId,
            cancellationToken);

        // Hidden, removed and draft listings stay invisible: reports must not
        // reveal that they exist.
        if (target is null)
        {
            return ListingReportResult.ListingNotFound();
        }

        if (target.OwnerId == reporterId)
        {
            return ListingReportResult.OwnListing();
        }

        // Duplicate reports follow the queue's active set: a report that is open
        // or already in review still blocks a new one from the same reporter.
        if (await reportRepository.HasActiveReportAsync(listingId, reporterId, cancellationToken))
        {
            return ListingReportResult.DuplicateOpenReport();
        }

        var now = DateTime.UtcNow;
        var windowStart = now - RateLimitWindow;

        // Soft limit: the count and the insert are separate statements, so
        // simultaneous submissions can exceed the cap by the race width.
        var recentReportTimes = await reportRepository.GetRecentReportTimesAsync(
            reporterId,
            windowStart,
            cancellationToken);

        if (recentReportTimes.Count >= MaximumReportsPerUserPerHour)
        {
            var retryAfter = recentReportTimes[0] + RateLimitWindow - now;
            return ListingReportResult.RateLimited(retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.Zero);
        }

        var report = new ListingReport
        {
            Id = Guid.NewGuid(),
            ListingId = listingId,
            ReporterId = reporterId,
            Reason = reason,
            Details = details,
            Status = ListingReportStatus.Open,
            CreatedAt = now
        };

        // The partial unique index (listing_id, reporter_id) WHERE status IN
        // ('open', 'in_review') guards two simultaneous submissions.
        return await reportRepository.TryAddAsync(report, cancellationToken)
            ? ListingReportResult.Created(report.Id)
            : ListingReportResult.DuplicateOpenReport();
    }
}
