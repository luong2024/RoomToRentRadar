using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Models.Moderation;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Moderation;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.SqliteDataAccess.Repositories;

public sealed class ModerationQueueRepository(AppDbContext dbContext) : IModerationQueueRepository
{
    public async Task<IReadOnlyList<ModerationGroupRow>> LoadGroupPageAsync(
        int take,
        ModerationQueueCursor? cursor,
        CancellationToken cancellationToken)
    {
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

    public async Task<IReadOnlyList<ModerationListingSummary>> GetListingsAsync(
        IReadOnlyCollection<Guid> listingIds,
        CancellationToken cancellationToken)
        => await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listingIds.Contains(listing.Id))
            .Select(listing => new ModerationListingSummary(
                listing.Id,
                listing.Title,
                listing.Status))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ModerationReasonCountRow>> GetActiveReasonCountsAsync(
        IReadOnlyCollection<Guid> listingIds,
        CancellationToken cancellationToken)
        => await ActiveReports()
            .AsNoTracking()
            .Where(report => listingIds.Contains(report.ListingId))
            .GroupBy(report => new { report.ListingId, report.Reason })
            .Select(group => new ModerationReasonCountRow(
                group.Key.ListingId,
                group.Key.Reason,
                group.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ModerationActiveClaim>> GetActiveClaimsAsync(
        IReadOnlyCollection<Guid> listingIds,
        DateTime utcNow,
        CancellationToken cancellationToken)
        => await ActiveReports()
            .AsNoTracking()
            .Where(report => listingIds.Contains(report.ListingId)
                && report.ClaimedByUserId != null
                && report.ClaimedUntilUtc > utcNow)
            .Select(report => new ModerationActiveClaim(
                report.ListingId,
                report.ClaimedByUserId!.Value,
                report.ClaimedUntilUtc!.Value))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetActiveReportIdsAsync(
        Guid listingId,
        int take,
        CancellationToken cancellationToken)
        => await ActiveReports()
            .AsNoTracking()
            .Where(report => report.ListingId == listingId)
            .OrderBy(report => report.CreatedAt)
            .ThenBy(report => report.Id)
            .Select(report => report.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ModerationListingDto>> GetListingQueueAsync(
        ListingStatus status,
        ModerationListingQueueCursor? cursor,
        int take,
        CancellationToken cancellationToken)
    {
        var listings = dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Status == status);

        if (status == ListingStatus.Published)
        {
            if (cursor is not null)
            {
                var updatedAt = cursor.CreatedAt;
                var listingId = cursor.ListingId;
                listings = listings.Where(listing =>
                    listing.UpdatedAt < updatedAt
                    || (listing.UpdatedAt == updatedAt && listing.Id.CompareTo(listingId) < 0));
            }

            listings = listings
                .OrderByDescending(listing => listing.UpdatedAt)
                .ThenByDescending(listing => listing.Id);
        }
        else
        {
            if (cursor is not null)
            {
                var createdAt = cursor.CreatedAt;
                var listingId = cursor.ListingId;
                listings = listings.Where(listing =>
                    listing.CreatedAt > createdAt
                    || (listing.CreatedAt == createdAt && listing.Id.CompareTo(listingId) > 0));
            }

            listings = listings
                .OrderBy(listing => listing.CreatedAt)
                .ThenBy(listing => listing.Id);
        }

        var page = await listings
            .Take(take)
            .ToListAsync(cancellationToken);

        if (page.Count == 0)
        {
            return [];
        }

        var listingIds = page.Select(listing => listing.Id).ToList();
        var photos = await dbContext.ListingPhotos
            .AsNoTracking()
            .Where(photo => listingIds.Contains(photo.ListingId))
            .OrderByDescending(photo => photo.IsCover)
            .ThenBy(photo => photo.SortOrder)
            .ThenBy(photo => photo.Id)
            .Select(photo => new ListingPhotoDto(
                photo.Id,
                photo.ListingId,
                photo.Url,
                photo.ThumbUrl,
                photo.Width,
                photo.Height,
                photo.Kind,
                photo.Alt,
                photo.SortOrder,
                photo.IsCover))
            .ToListAsync(cancellationToken);

        var photosByListing = photos
            .GroupBy(photo => photo.ListingId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ListingPhotoDto>)group.ToList());

        return page
            .Select(listing => new ModerationListingDto(
                listing.Id,
                listing.Title,
                listing.OwnerId,
                listing.Status,
                listing.CreatedAt,
                listing.UpdatedAt,
                listing.Description,
                listing.Price,
                listing.Deposit,
                listing.AreaM2,
                listing.RoomType,
                listing.Bedrooms,
                listing.Address,
                listing.Lat,
                listing.Lng,
                listing.LandlordName,
                listing.LandlordPhone,
                photosByListing.TryGetValue(listing.Id, out var listingPhotos)
                    ? listingPhotos
                    : []))
            .ToList();
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
}
