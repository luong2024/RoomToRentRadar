using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
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
    /// <summary>
    /// Report ids are a bounded preview of the group; open the report detail
    /// endpoint for the reports beyond this limit.
    /// </summary>
    public const int MaximumReportIdsPerGroup = 50;

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

        var reasonCounts = await ActiveReports()
            .AsNoTracking()
            .Where(report => listingIds.Contains(report.ListingId))
            .GroupBy(report => new { report.ListingId, report.Reason })
            .Select(group => new
            {
                group.Key.ListingId,
                group.Key.Reason,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var reasonsByListing = reasonCounts
            .GroupBy(row => row.ListingId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ModerationReasonCountDto>)group
                    .OrderByDescending(row => row.Count)
                    .ThenBy(row => row.Reason)
                    .Select(row => new ModerationReasonCountDto(row.Reason, row.Count))
                    .ToList());

        var activeClaims = await ActiveReports()
            .AsNoTracking()
            .Where(report => listingIds.Contains(report.ListingId)
                && report.ClaimedByUserId != null
                && report.ClaimedUntilUtc > now)
            .Select(report => new
            {
                report.ListingId,
                report.ClaimedByUserId,
                report.ClaimedUntilUtc
            })
            .ToListAsync(cancellationToken);

        var claimsByListing = activeClaims
            .GroupBy(claim => claim.ListingId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(claim => claim.ClaimedUntilUtc).First());

        var items = new List<ModerationReportGroupDto>(rows.Count);
        foreach (var row in rows)
        {
            // A moderator can resolve a group between the page query and these
            // follow-up queries; skip groups that no longer have a listing
            // instead of failing the whole request.
            if (!listings.TryGetValue(row.ListingId, out var listing))
            {
                continue;
            }

            var reportIds = await ActiveReports()
                .AsNoTracking()
                .Where(report => report.ListingId == row.ListingId)
                .OrderBy(report => report.CreatedAt)
                .ThenBy(report => report.Id)
                .Select(report => report.Id)
                .Take(MaximumReportIdsPerGroup)
                .ToListAsync(cancellationToken);

            claimsByListing.TryGetValue(row.ListingId, out var claim);
            var reasons = reasonsByListing.TryGetValue(row.ListingId, out var countedReasons)
                ? countedReasons
                : [];

            items.Add(new ModerationReportGroupDto(
                row.ListingId,
                listing.Title,
                listing.Status,
                row.TotalReports,
                reasons,
                row.OldestReportAt,
                claim?.ClaimedByUserId,
                claim?.ClaimedUntilUtc,
                reportIds));
        }

        return new ModerationReportQueueDto(items, pagination);
    }

    /// <summary>
    /// Lists listings for one status, oldest first, so moderators see the
    /// longest-waiting pending-review listings before approving them.
    /// </summary>
    public async Task<ModerationListingQueueDto> GetListingQueueAsync(
        ModerationListingQueueQuery query,
        ModerationListingQueueCursor? cursor,
        CancellationToken cancellationToken)
    {
        var listings = dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Status == query.Status);

        if (cursor is not null)
        {
            var createdAt = cursor.CreatedAt;
            var listingId = cursor.ListingId;
            listings = listings.Where(listing =>
                listing.CreatedAt > createdAt
                || (listing.CreatedAt == createdAt && listing.Id.CompareTo(listingId) > 0));
        }

        var rows = await listings
            .OrderBy(listing => listing.CreatedAt)
            .ThenBy(listing => listing.Id)
            .Take(query.Limit + 1)
            .Select(listing => new ModerationListingDto(
                listing.Id,
                listing.Title,
                listing.OwnerId,
                listing.Status,
                listing.CreatedAt,
                listing.UpdatedAt))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > query.Limit;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        string? nextCursor = null;
        if (hasMore && rows.Count > 0)
        {
            var last = rows[^1];
            nextCursor = ModerationListingQueueCursorCodec.Encode(
                last.Status, last.CreatedAt, last.Id);
        }

        return new ModerationListingQueueDto(
            rows, new PaginationInfo(query.Limit, nextCursor, hasMore));
    }

    public Task<ModerationReportDetailDto?> GetReportAsync(
        Guid reportId,
        CancellationToken cancellationToken)
        => (from report in dbContext.ListingReports.AsNoTracking()
            join listing in dbContext.Listings.AsNoTracking()
                on report.ListingId equals listing.Id
            where report.Id == reportId
            select new ModerationReportDetailDto(
                report.Id,
                report.ListingId,
                listing.Title,
                listing.Status,
                report.ReporterId,
                report.Reason,
                report.Details,
                report.Status,
                report.ClaimedByUserId,
                report.ClaimedUntilUtc,
                report.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<ListingReport> ActiveReports()
        => dbContext.ListingReports.Where(report => report.Status == ListingReportStatus.Open
            || report.Status == ListingReportStatus.InReview);

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
