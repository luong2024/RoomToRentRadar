using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Shared.Favourites;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Favourites;

public sealed class FavouriteService(AppDbContext dbContext)
{
    public async Task<IReadOnlyList<FavouriteListingDto>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Favourites
            .AsNoTracking()
            .Where(favourite => favourite.UserId == userId
                && (favourite.Listing!.Status == ListingStatus.Published
                    || favourite.Listing.Status == ListingStatus.Hidden
                    || favourite.Listing.Status == ListingStatus.Removed))
            .OrderByDescending(favourite => favourite.CreatedAt)
            .ThenBy(favourite => favourite.ListingId)
            .Select(favourite => new FavouriteListingDto(
                favourite.ListingId,
                favourite.Listing!.Title,
                favourite.Listing.Price,
                favourite.Listing.AreaM2,
                favourite.Listing.Bedrooms,
                favourite.Listing.Address,
                favourite.Listing.Lat,
                favourite.Listing.Lng,
                favourite.Listing.Photos
                    .OrderByDescending(photo => photo.IsCover)
                    .ThenBy(photo => photo.SortOrder)
                    .ThenBy(photo => photo.Id)
                    .Select(photo => photo.ThumbUrl ?? photo.Url)
                    .FirstOrDefault(),
                favourite.Listing.Status == ListingStatus.Published,
                favourite.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Adds the listing when it is publicly visible. Returns false when no published
    /// listing exists, so the caller can answer 404 without revealing why.
    /// </summary>
    public async Task<bool> AddAsync(
        Guid userId,
        Guid listingId,
        CancellationToken cancellationToken)
    {
        var isVisible = await dbContext.Listings
            .AnyAsync(
                listing => listing.Id == listingId && listing.Status == ListingStatus.Published,
                cancellationToken);
        if (!isVisible)
        {
            return false;
        }

        var exists = await dbContext.Favourites
            .AnyAsync(
                favourite => favourite.UserId == userId && favourite.ListingId == listingId,
                cancellationToken);
        if (exists)
        {
            return true;
        }

        dbContext.Favourites.Add(new Favourite
        {
            UserId = userId,
            ListingId = listingId,
            CreatedAt = DateTime.UtcNow
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent request may have inserted the same pair first. The
            // composite key makes that failure harmless for an idempotent PUT.
            var inserted = await dbContext.Favourites
                .AnyAsync(
                    favourite => favourite.UserId == userId && favourite.ListingId == listingId,
                    cancellationToken);
            if (!inserted)
            {
                throw;
            }
        }

        return true;
    }

    public async Task RemoveAsync(
        Guid userId,
        Guid listingId,
        CancellationToken cancellationToken)
    {
        var favourite = await dbContext.Favourites
            .FirstOrDefaultAsync(
                candidate => candidate.UserId == userId && candidate.ListingId == listingId,
                cancellationToken);
        if (favourite is null)
        {
            return;
        }

        dbContext.Favourites.Remove(favourite);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
