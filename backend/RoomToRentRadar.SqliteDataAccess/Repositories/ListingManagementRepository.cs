using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;

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
            .SingleOrDefaultAsync(
                x => x.Id == listingId && x.OwnerId == ownerId,
                cancellationToken);

    public void Add(Listing listing) => dbContext.Listings.Add(listing);

    public void AddPhotoRange(IReadOnlyList<ListingPhoto> photos) => dbContext.ListingPhotos.AddRange(photos);
}
