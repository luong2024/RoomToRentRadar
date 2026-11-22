using RoomToRentRadar.Ports.Models.Common;
using RoomToRentRadar.Ports.Models.Photos;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Ports.Inbound;

public interface IListingPhotosUseCase
{
    Task<UseCaseResult<IReadOnlyList<ListingPhotoDto>>> UploadAsync(
        Guid listingId,
        Guid ownerId,
        ListingPhotoKind kind,
        IReadOnlyList<PhotoUpload> files,
        CancellationToken cancellationToken);

    Task<UseCaseResult<IReadOnlyList<ListingPhotoDto>>> GetGalleryAsync(
        Guid listingId,
        Guid? viewerId,
        CancellationToken cancellationToken);
}
