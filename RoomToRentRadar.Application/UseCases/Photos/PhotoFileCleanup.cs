using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Application.UseCases.Photos;

/// <summary>
/// Best-effort removal of the physical files behind a deleted <see cref="RoomPhoto"/> row
/// (BR-MED-03). The database is the source of truth: a storage failure must not turn a committed
/// delete into an error, so every exception is swallowed.
/// </summary>
internal static class PhotoFileCleanup
{
    public static async Task DeleteAsync(IPhotoStoragePort storage, RoomPhoto photo)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(photo);

        foreach (var storageKey in DistinctKeys(photo))
        {
            try
            {
                await storage.DeleteAsync(storageKey, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Best-effort cleanup: an orphaned file is harmless, a failed request is not.
            }
        }
    }

    public static async Task DeleteManyAsync(IPhotoStoragePort storage, IEnumerable<RoomPhoto> photos)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(photos);

        foreach (var photo in photos)
        {
            await DeleteAsync(storage, photo).ConfigureAwait(false);
        }
    }

    private static IEnumerable<string> DistinctKeys(RoomPhoto photo) =>
        new[] { photo.OriginalPath, photo.MediumPath, photo.ThumbPath }
            .Distinct(StringComparer.Ordinal);
}
