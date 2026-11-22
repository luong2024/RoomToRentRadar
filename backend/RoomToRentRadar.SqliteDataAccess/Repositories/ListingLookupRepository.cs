using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Models.Listings;
using RoomToRentRadar.Ports.Models.Reports;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.SqliteDataAccess.Repositories;

public sealed class ListingLookupRepository(AppDbContext dbContext) : IListingLookupRepository
{
    public Task<bool> ExistsAsync(Guid listingId, CancellationToken cancellationToken)
        => dbContext.Listings
            .AsNoTracking()
            .AnyAsync(listing => listing.Id == listingId, cancellationToken);

    public async Task<ListingReportTarget?> FindPublishedReportTargetAsync(
        Guid listingId,
        CancellationToken cancellationToken)
        => await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId && listing.Status == ListingStatus.Published)
            .Select(listing => new ListingReportTarget(listing.OwnerId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ListingLocation?> FindPublishedLocationAsync(
        Guid listingId,
        CancellationToken cancellationToken)
        => await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId && listing.Status == ListingStatus.Published)
            .Select(listing => new ListingLocation(listing.Lat, listing.Lng))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PublishedListing>> GetPublishedUpdatedSinceAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
        => await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Status == ListingStatus.Published
                && listing.UpdatedAt >= cutoffUtc)
            .OrderBy(listing => listing.UpdatedAt)
            .ThenBy(listing => listing.Id)
            .Select(listing => new PublishedListing(
                listing.Id,
                listing.Title,
                listing.Price,
                listing.AreaM2,
                listing.Lat,
                listing.Lng,
                listing.UpdatedAt))
            .ToListAsync(cancellationToken);
}
