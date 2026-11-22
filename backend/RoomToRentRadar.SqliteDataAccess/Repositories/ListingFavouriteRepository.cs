using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadar.SqliteDataAccess.Repositories;

public sealed class ListingFavouriteRepository(AppDbContext dbContext) : IListingFavouriteRepository
{
    public async Task<IReadOnlyList<FavouriteRoomDto>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
        => await dbContext.ListingFavourites
            .AsNoTracking()
            .Where(favourite => favourite.UserId == userId)
            .OrderByDescending(favourite => favourite.CreatedAt)
            .ThenBy(favourite => favourite.ListingId)
            .Join(
                dbContext.Listings.AsNoTracking(),
                favourite => favourite.ListingId,
                listing => listing.Id,
                (favourite, listing) => new FavouriteRoomDto(
                    listing.Id,
                    listing.Title,
                    listing.Price,
                    listing.AreaM2,
                    listing.Bedrooms,
                    listing.Address,
                    listing.Status,
                    listing.Photos
                        .OrderByDescending(photo => photo.IsCover)
                        .ThenBy(photo => photo.SortOrder)
                        .ThenBy(photo => photo.Id)
                        .Select(photo => photo.ThumbUrl ?? photo.Url)
                        .FirstOrDefault(),
                    favourite.CreatedAt))
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid userId, Guid listingId, CancellationToken cancellationToken)
        => dbContext.ListingFavourites.AnyAsync(
            favourite => favourite.UserId == userId && favourite.ListingId == listingId,
            cancellationToken);

    public async Task AddAsync(ListingFavourite favourite, CancellationToken cancellationToken)
        => await dbContext.ListingFavourites.AddAsync(favourite, cancellationToken);

    public Task RemoveAsync(Guid userId, Guid listingId, CancellationToken cancellationToken)
        => dbContext.ListingFavourites
            .Where(favourite => favourite.UserId == userId && favourite.ListingId == listingId)
            .ExecuteDeleteAsync(cancellationToken);
}
