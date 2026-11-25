using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.SqliteDataAccess.Repositories;

public sealed class ListingManagementRepository(AppDbContext dbContext) : IListingManagementRepository
{
    public async Task<IReadOnlyList<Listing>> GetMineAsync(
        Guid ownerId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
        => await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.OwnerId == ownerId)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.Id)
            .Skip(offset)
            .Take(limit)
            .Include(x => x.Photos)
            .Include(x => x.Amenities)
            .Include(x => x.Rules)
            .Include(x => x.HouseRules)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public async Task<Listing?> FindOwnedAsync(
        Guid ownerId,
        Guid listingId,
        CancellationToken cancellationToken)
        => await dbContext.Listings
            .Include(x => x.Photos)
            .Include(x => x.Amenities)
            .Include(x => x.Rules)
            .Include(x => x.HouseRules)
            .AsSplitQuery()
            .SingleOrDefaultAsync(
                x => x.Id == listingId && x.OwnerId == ownerId,
                cancellationToken);

    public async Task<Listing?> GetManagedAsync(
        Guid ownerId,
        Guid listingId,
        CancellationToken cancellationToken)
        => await dbContext.Listings
            .AsNoTracking()
            .Include(x => x.Photos)
            .Include(x => x.Amenities)
            .Include(x => x.Rules)
            .Include(x => x.HouseRules)
            .AsSplitQuery()
            .SingleOrDefaultAsync(
                x => x.Id == listingId && x.OwnerId == ownerId,
                cancellationToken);

    public void Add(Listing listing) => dbContext.Listings.Add(listing);

    public void AddPhotoRange(IReadOnlyList<ListingPhoto> photos) => dbContext.ListingPhotos.AddRange(photos);

    public Task<bool> HasModerationAuditLogsAsync(
        Guid listingId,
        CancellationToken cancellationToken)
        => dbContext.ModerationAuditLogs.AnyAsync(x => x.ListingId == listingId, cancellationToken);

    public async Task CleanupExternalReferencesAsync(
        Guid listingId,
        CancellationToken cancellationToken)
    {
        await dbContext.SavedSearchAlertLogs
            .Where(x => x.ListingId == listingId)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.ListingFavourites
            .Where(x => x.ListingId == listingId)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.Notifications
            .Where(x => x.ListingId == listingId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.ListingId, (Guid?)null),
                cancellationToken);

        await dbContext.ListingReports
            .Where(x => x.ListingId == listingId
                && (x.Status == ListingReportStatus.Open || x.Status == ListingReportStatus.InReview))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, ListingReportStatus.Dismissed)
                    .SetProperty(x => x.ClaimedByUserId, (Guid?)null)
                    .SetProperty(x => x.ClaimedUntilUtc, (DateTime?)null)
                    .SetProperty(x => x.Version, x => x.Version + 1),
                cancellationToken);
    }

    public void Remove(Listing listing) => dbContext.Listings.Remove(listing);

    public void RemovePhotoRange(IEnumerable<ListingPhoto> photos) => dbContext.ListingPhotos.RemoveRange(photos);
}
