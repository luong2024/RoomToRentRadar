using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Moderation;
using RoomToRentRadar.Shared.Pagination;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.Api.Features.Moderation;

/// <summary>
/// Groups every open and in-review report by listing. The grouped page is a single
/// SQL statement (GROUP BY + HAVING keyset + LIMIT); listing details, reason
/// breakdowns and report ids are then loaded for the page's listings only.
/// </summary>
public sealed class ModerationQueueService(AppDbContext dbContext)
{
    public async Task<ModerationReportQueueDto> GetQueueAsync(
        ModerationQueueQuery query,
        ModerationQueueCursor? cursor,
        CancellationToken cancellationToken)
    {
        var rows = await LoadGroupPageAsync(query, cursor, cancellationToken);

        // The raw SQL projection bypasses UtcDateTimeConverter, so the reader
        // returns Unspecified kinds even though the stored values are UTC.
        foreach (var row in rows)
        {
            row.OldestReportAt = DateTime.SpecifyKind(row.OldestReportAt, DateTimeKind.Utc);
        }

        var hasMore = rows.Count > query.Limit;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        string? nextCursor = null;
        if (hasMore && rows.Count > 0)
        {
            var last = rows[^1];
            nextCursor = ModerationQueueCursorCodec.Encode(
                last.TotalReports, last.OldestReportAt, last.ListingId);
        }

        var pagination = new PaginationInfo(query.Limit, nextCursor, hasMore);
        if (rows.Count == 0)
        {
            return new ModerationReportQueueDto([], pagination);
        }

        var listingIds = rows.Select(row => row.ListingId).ToList();
        var now = DateTime.UtcNow;

        var listings = await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listingIds.Contains(listing.Id))
            .Select(listing => new { listing.Id, listing.Title, listing.Status })
            .ToDictionaryAsync(listing => listing.Id, cancellationToken);

        var activeReports = await dbContext.ListingReports
            .AsNoTracking()
            .Where(report => listingIds.Contains(report.ListingId)
                && (report.Status == ListingReportStatus.Open
                    || report.Status == ListingReportStatus.InReview))
            .Select(report => new
            {
                report.Id,
                report.ListingId,
                report.Reason,
                report.CreatedAt,
                report.ClaimedByUserId,
                report.ClaimedUntilUtc
            })
            .ToListAsync(cancellationToken);

        var reportsByListing = activeReports
            .GroupBy(report => report.ListingId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var items = new List<ModerationReportGroupDto>(rows.Count);
        foreach (var row in rows)
        {
            var listing = listings[row.ListingId];
            var reports = reportsByListing[row.ListingId];

            var activeClaim = reports
                .Where(report => report.ClaimedByUserId is not null
                    && report.ClaimedUntilUtc is not null
                    && report.ClaimedUntilUtc > now)
                .OrderByDescending(report => report.ClaimedUntilUtc)
                .FirstOrDefault();

            var reasons = reports
                .GroupBy(report => report.Reason)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Select(group => new ModerationReasonCountDto(group.Key, group.Count()))
                .ToList();

            var reportIds = reports
                .OrderBy(report => report.CreatedAt)
                .ThenBy(report => report.Id)
                .Select(report => report.Id)
                .ToList();

            items.Add(new ModerationReportGroupDto(
                row.ListingId,
                listing.Title,
                listing.Status,
                row.TotalReports,
                reasons,
                row.OldestReportAt,
                activeClaim?.ClaimedByUserId,
                activeClaim?.ClaimedUntilUtc,
                reportIds));
        }

        return new ModerationReportQueueDto(items, pagination);
    }

    private async Task<List<ModerationGroupRow>> LoadGroupPageAsync(
        ModerationQueueQuery query,
        ModerationQueueCursor? cursor,
        CancellationToken cancellationToken)
    {
        var take = query.Limit + 1;

        // Parameters compare against the fixed-format SQLite TEXT timestamps, so
        // the ordering is chronological and matches the cursor exactly.
        FormattableString sql = $"""
            SELECT listing_id AS "ListingId",
                   COUNT(*) AS "TotalReports",
                   MIN(created_at) AS "OldestReportAt"
            FROM listing_reports
            WHERE status IN ('open', 'in_review')
            GROUP BY listing_id
            HAVING {cursor?.TotalReports} IS NULL
                OR COUNT(*) < {cursor?.TotalReports}
                OR (COUNT(*) = {cursor?.TotalReports} AND MIN(created_at) > {cursor?.OldestReportAt})
                OR (COUNT(*) = {cursor?.TotalReports}
                    AND MIN(created_at) = {cursor?.OldestReportAt}
                    AND listing_id > {cursor?.ListingId})
            ORDER BY COUNT(*) DESC, MIN(created_at) ASC, listing_id ASC
            LIMIT {take}
            """;

        return await dbContext.Database
            .SqlQuery<ModerationGroupRow>(sql)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Row shape of the grouped queue page; matched to the raw SQL aliases.</summary>
public sealed class ModerationGroupRow
{
    public Guid ListingId { get; set; }
    public int TotalReports { get; set; }
    public DateTime OldestReportAt { get; set; }
}
