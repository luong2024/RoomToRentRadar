using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Models.Moderation;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Moderation;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.UseCases.Moderation;

/// <summary>
/// Groups every open and in-review report by listing. The grouped page is a single
/// SQL statement (GROUP BY + HAVING keyset + LIMIT); listing details, reason
/// breakdowns and report ids are then loaded for the page's listings only.
/// </summary>
public sealed class ModerationQueueService(IModerationQueueRepository queueRepository)
    : IModerationQueueUseCase
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
        var rows = (await queueRepository.LoadGroupPageAsync(
            query.Limit + 1,
            cursor,
            cancellationToken)).ToList();

        // The raw SQL projection bypasses the UTC-kind converter, so the rows
        // arrive as Unspecified even though the stored values are UTC.
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

        var listings = (await queueRepository.GetListingsAsync(listingIds, cancellationToken))
            .ToDictionary(listing => listing.Id);

        var reasonCounts = await queueRepository.GetActiveReasonCountsAsync(
            listingIds, cancellationToken);

        var reasonsByListing = reasonCounts
            .GroupBy(row => row.ListingId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ModerationReasonCountDto>)group
                    .OrderByDescending(row => row.Count)
                    .ThenBy(row => row.Reason)
                    .Select(row => new ModerationReasonCountDto(row.Reason, row.Count))
                    .ToList());

        var activeClaims = await queueRepository.GetActiveClaimsAsync(
            listingIds, now, cancellationToken);

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

            var reportIds = await queueRepository.GetActiveReportIdsAsync(
                row.ListingId,
                MaximumReportIdsPerGroup,
                cancellationToken);

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
    /// Lists listings for one status: <see cref="ListingStatus.Published"/> orders by
    /// <c>UpdatedAt DESC, Id DESC</c> so newly approved listings appear first, while
    /// <see cref="ListingStatus.PendingReview"/> and other statuses order by
    /// <c>CreatedAt ASC, Id ASC</c> for FIFO review.
    /// </summary>
    public async Task<ModerationListingQueueDto> GetListingQueueAsync(
        ModerationListingQueueQuery query,
        ModerationListingQueueCursor? cursor,
        CancellationToken cancellationToken)
    {
        var rows = (await queueRepository.GetListingQueueAsync(
            query.Status,
            cursor,
            query.Limit + 1,
            cancellationToken)).ToList();

        var hasMore = rows.Count > query.Limit;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        string? nextCursor = null;
        if (hasMore && rows.Count > 0)
        {
            var last = rows[^1];
            var sortTimestamp = query.Status == ListingStatus.Published
                ? last.UpdatedAt
                : last.CreatedAt;
            nextCursor = ModerationListingQueueCursorCodec.Encode(
                last.Status, sortTimestamp, last.Id);
        }

        return new ModerationListingQueueDto(
            rows, new PaginationInfo(query.Limit, nextCursor, hasMore));
    }

    public Task<ModerationReportDetailDto?> GetReportAsync(
        Guid reportId,
        CancellationToken cancellationToken)
        => queueRepository.GetReportAsync(reportId, cancellationToken);
}
