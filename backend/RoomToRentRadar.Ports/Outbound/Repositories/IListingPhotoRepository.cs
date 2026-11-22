using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Models.Photos;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IListingPhotoRepository
{
    Task<PhotoListingInfo?> FindListingAsync(Guid listingId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PhotoSlot>> GetSlotsAsync(Guid listingId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ListingPhotoDto>> GetGalleryAsync(
        Guid listingId,
        CancellationToken cancellationToken);

    void AddRange(IReadOnlyList<ListingPhoto> photos);
}
