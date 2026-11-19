using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Photos;

/// <summary>
/// BR-MED-03 hourly sweep: unattached uploads older than 24 h are deleted from <c>RoomPhotos</c> and
/// their files removed. Returns the number of photos actually removed; a row that was attached in the
/// meantime is skipped by the repository's conditional delete.
/// </summary>
public sealed class CleanupExpiredPhotosUseCase : ICleanupExpiredPhotosUseCase
{
    private readonly IRoomPhotoRepository _photoRepository;
    private readonly IPhotoStoragePort _storage;

    public CleanupExpiredPhotosUseCase(IRoomPhotoRepository photoRepository, IPhotoStoragePort storage)
    {
        ArgumentNullException.ThrowIfNull(photoRepository);
        ArgumentNullException.ThrowIfNull(storage);

        _photoRepository = photoRepository;
        _storage = storage;
    }

    public async Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        var cutoffUtc = DateTime.UtcNow - PhotoFileRules.UnattachedPhotoLifetime;
        var expiredPhotos = await _photoRepository.ListExpiredUnattachedAsync(cutoffUtc, ct).ConfigureAwait(false);

        var removed = 0;
        foreach (var photo in expiredPhotos)
        {
            var attempt = await _photoRepository
                .DeleteUnattachedAsync(photo.Id, photo.UploadedByUserId, ct)
                .ConfigureAwait(false);

            if (attempt.Outcome != PhotoDeleteOutcome.Deleted)
            {
                continue;
            }

            if (attempt.Photo is not null)
            {
                await PhotoFileCleanup.DeleteAsync(_storage, attempt.Photo).ConfigureAwait(false);
            }

            removed++;
        }

        return removed;
    }
}
