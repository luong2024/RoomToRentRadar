using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadar.Api.Features.Favourites;

public sealed class FavouritesService(AppDbContext dbContext)
{
    /// <summary>
    /// Joins the user's favourites with the live listing rows so the price and
    /// availability shown always reflect the current listing state (AC3).
    /// </summary>
    public async Task<IReadOnlyList<FavouriteRoomDto>> GetAsync(
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

    /// <summary>
    /// Returns <c>false</c> when the listing does not exist. Adding an already
    /// favourited listing is a no-op so the operation stays idempotent.
    /// </summary>
    public async Task<bool> AddAsync(
        Guid userId,
        Guid listingId,
        CancellationToken cancellationToken)
    {
        var listingExists = await dbContext.Listings
            .AsNoTracking()
            .AnyAsync(listing => listing.Id == listingId, cancellationToken);
        if (!listingExists)
        {
            return false;
        }

        var alreadyFavourited = await dbContext.ListingFavourites
            .AnyAsync(
                favourite => favourite.UserId == userId && favourite.ListingId == listingId,
                cancellationToken);
        if (!alreadyFavourited)
        {
            dbContext.ListingFavourites.Add(new ListingFavourite
            {
                UserId = userId,
                ListingId = listingId,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public Task RemoveAsync(Guid userId, Guid listingId, CancellationToken cancellationToken)
        => dbContext.ListingFavourites
            .Where(favourite => favourite.UserId == userId && favourite.ListingId == listingId)
            .ExecuteDeleteAsync(cancellationToken);
}
