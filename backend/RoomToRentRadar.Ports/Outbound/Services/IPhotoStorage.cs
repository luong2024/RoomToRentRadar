using RoomToRentRadar.Ports.Models.Photos;

namespace RoomToRentRadar.Ports.Outbound.Services;

public interface IPhotoStorage
{
    string GetOriginalUrl(Guid listingId, Guid photoId);

    string GetThumbnailUrl(Guid listingId, Guid photoId);

    Task SaveAsync(
        Guid listingId,
        Guid photoId,
        byte[] original,
        byte[] thumbnail,
        CancellationToken cancellationToken);

    void Delete(Guid listingId, Guid photoId);
}

public interface IPhotoProcessor
{
    bool TryProcess(byte[] content, out ProcessedPhoto? photo);
}
