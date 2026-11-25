using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Ports.Outbound;

public interface IRoomPhotoStorageService
{
    Task<StoredPhotoResult> UploadAsync(
        string roomId,
        string photoId,
        ProcessedImageResult processedImage,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string objectKey,
        string thumbnailObjectKey,
        CancellationToken cancellationToken = default);
}
