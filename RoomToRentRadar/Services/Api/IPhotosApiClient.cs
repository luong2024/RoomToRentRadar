using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Services.Api;

public interface IPhotosApiClient
{
    Task<ListingPhotosFetchResult> GetPhotosAsync(
        Guid listingId,
        CancellationToken cancellationToken = default);

    Task<ListingPhotosUploadResult> UploadPhotosAsync(
        Guid listingId,
        IReadOnlyList<ListingPhotoUpload> files,
        ListingPhotoKind kind,
        CancellationToken cancellationToken = default);

    Task<byte[]?> GetMediaBytesAsync(
        string absoluteUrl,
        CancellationToken cancellationToken = default);
}
