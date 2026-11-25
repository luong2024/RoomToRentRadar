namespace RoomToRentRadar.Services.Api;

public interface IPhotosApiClient
{
    Task<ListingPhotosFetchResult> GetPhotosAsync(
        Guid listingId,
        CancellationToken cancellationToken = default);

    Task<byte[]?> GetMediaBytesAsync(
        string absoluteUrl,
        CancellationToken cancellationToken = default);
}
