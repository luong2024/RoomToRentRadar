using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Photos;

// PhotoCleanupService — BR-MED-03: delete unattached photos older than 24 h.
public sealed class CleanupExpiredPhotosUseCase : ICleanupExpiredPhotosUseCase
{
    private const int BatchSize = 200;

    private readonly IRoomPhotoRepository _repository;
    private readonly IPhotoStoragePort _storage;

    public CleanupExpiredPhotosUseCase(IRoomPhotoRepository repository, IPhotoStoragePort storage)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(storage);

        _repository = repository;
        _storage = storage;
    }

    public async Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        var expired = await _repository.ListExpiredUnattachedAsync(
            DateTime.UtcNow.Subtract(PhotoFileRules.UnattachedPhotoLifetime),
            BatchSize,
            ct);

        var deleted = 0;
        foreach (var photo in expired)
        {
            ct.ThrowIfCancellationRequested();

            // Metadata delete is the race-safe gate: a photo attached since the listing is skipped.
            var outcome = await _repository.DeleteUnattachedAsync(photo.Id, photo.UploadedByUserId, ct);
            if (outcome != RoomPhotoDeleteOutcome.Deleted)
            {
                continue;
            }

            await DeleteFilesBestEffortAsync(photo);
            deleted++;
        }

        return deleted;
    }

    private async Task DeleteFilesBestEffortAsync(RoomPhoto photo)
    {
        var paths = new[] { photo.OriginalPath, photo.MediumPath, photo.ThumbPath }
            .Distinct(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            try
            {
                await _storage.DeleteAsync(path, CancellationToken.None);
            }
            catch
            {
                // Metadata is already gone; a storage hiccup must not stop the batch.
            }
        }
    }
}
