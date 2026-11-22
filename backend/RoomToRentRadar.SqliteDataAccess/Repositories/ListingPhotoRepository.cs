using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Models.Photos;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.SqliteDataAccess.Repositories;

public sealed class ListingPhotoRepository(AppDbContext dbContext) : IListingPhotoRepository
{
    public async Task<PhotoListingInfo?> FindListingAsync(
        Guid listingId,
        CancellationToken cancellationToken)
        => await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId)
            .Select(listing => new PhotoListingInfo(listing.OwnerId, listing.Status))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PhotoSlot>> GetSlotsAsync(
        Guid listingId,
        CancellationToken cancellationToken)
        => await dbContext.ListingPhotos
            .Where(photo => photo.ListingId == listingId)
            .Select(photo => new PhotoSlot(photo.SortOrder, photo.IsCover))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ListingPhotoDto>> GetGalleryAsync(
        Guid listingId,
        CancellationToken cancellationToken)
        => await dbContext.ListingPhotos
            .AsNoTracking()
            .Where(photo => photo.ListingId == listingId)
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

    public void AddRange(IReadOnlyList<ListingPhoto> photos)
        => dbContext.ListingPhotos.AddRange(photos);
}
