using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.Api.Features.Reports;

public sealed class ListingReportsService(AppDbContext dbContext)
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
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Where(candidate => candidate.Id == listingId)
            .Select(candidate => new { candidate.Status, candidate.OwnerId })
            .FirstOrDefaultAsync(cancellationToken);

        // Hidden, removed and draft listings stay invisible: reports must not
        // reveal that they exist.
        if (listing is null || listing.Status != ListingStatus.Published)
        {
            return ListingReportResult.ListingNotFound();
        }

        if (listing.OwnerId == reporterId)
        {
            return ListingReportResult.OwnListing();
        }

        // Duplicate reports follow the queue's active set: a report that is open
        // or already in review still blocks a new one from the same reporter.
        var hasActiveReport = await dbContext.ListingReports
            .AnyAsync(
                report => report.ListingId == listingId
                    && report.ReporterId == reporterId
                    && (report.Status == ListingReportStatus.Open
                        || report.Status == ListingReportStatus.InReview),
                cancellationToken);
        if (hasActiveReport)
        {
            return ListingReportResult.DuplicateOpenReport();
        }

        var now = DateTime.UtcNow;
        var windowStart = now - RateLimitWindow;

        // Soft limit: the count and the insert are separate statements, so
        // simultaneous submissions can exceed the cap by the race width.
        var recentReportTimes = await dbContext.ListingReports
            .AsNoTracking()
            .Where(report => report.ReporterId == reporterId && report.CreatedAt >= windowStart)
            .OrderBy(report => report.CreatedAt)
            .Select(report => report.CreatedAt)
            .ToListAsync(cancellationToken);

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
        dbContext.ListingReports.Add(report);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            // The partial unique index (listing_id, reporter_id) WHERE status IN
            // ('open', 'in_review') also guards two simultaneous submissions.
            return ListingReportResult.DuplicateOpenReport();
        }

        return ListingReportResult.Created(report.Id);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        => exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19, // SQLITE_CONSTRAINT
            SqliteExtendedErrorCode: 2067 // SQLITE_CONSTRAINT_UNIQUE
        };
}
