using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Adapters.Outbound.Storage;

/// <summary>
/// Fail-closed storage adapter used when photo storage is not configured in non-development environments.
/// Allows unrelated API endpoints to start normally while rejecting photo mutations with 503 Service Unavailable
/// instead of returning unusable .local URLs.
/// </summary>
public sealed class UnconfiguredRoomPhotoStorageService : IRoomPhotoStorageService
{
    private readonly string _reason;

    public UnconfiguredRoomPhotoStorageService(string reason)
    {
        _reason = string.IsNullOrWhiteSpace(reason)
            ? "Photo storage is not configured for this environment."
            : reason;
    }

    public Task<StoredPhotoResult> UploadAsync(
        string roomId,
        string photoId,
        ProcessedImageResult processedImage,
        CancellationToken cancellationToken = default) =>
        throw new PhotoStorageNotConfiguredException(_reason);

    public Task DeleteAsync(
        string objectKey,
        string thumbnailObjectKey,
        CancellationToken cancellationToken = default) =>
        throw new PhotoStorageNotConfiguredException(_reason);
}
