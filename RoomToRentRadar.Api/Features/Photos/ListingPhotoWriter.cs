using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Photos;

public sealed record ListingPhotoWriteResult(
    bool Succeeded,
    IReadOnlyList<ListingPhoto> Photos,
    IReadOnlyList<Guid> WrittenPhotoIds);

public sealed class ListingPhotoWriter(PhotoStorage photoStorage, PhotoProcessor photoProcessor)
{
    public async Task<ListingPhotoWriteResult> WriteAsync(
        Guid listingId,
        IReadOnlyList<IFormFile> files,
        int startSortOrder,
        bool hasCover,
        ListingPhotoKind kind,
        CancellationToken cancellationToken)
    {
        var photos = new List<ListingPhoto>(files.Count);
        var writtenPhotoIds = new List<Guid>(files.Count);

        try
        {
            for (var index = 0; index < files.Count; index++)
            {
                var content = await ReadAsync(files[index], cancellationToken);
                if (!photoProcessor.TryProcess(content, out var processed))
                {
                    Delete(listingId, writtenPhotoIds);
                    return new ListingPhotoWriteResult(false, [], []);
                }

                var photoId = Guid.NewGuid();
                await photoStorage.SaveAsync(
                    listingId,
                    photoId,
                    processed!.Original,
                    processed.Thumbnail,
                    cancellationToken);
                writtenPhotoIds.Add(photoId);

                photos.Add(new ListingPhoto
                {
                    Id = photoId,
                    ListingId = listingId,
                    Url = photoStorage.GetOriginalUrl(listingId, photoId),
                    ThumbUrl = photoStorage.GetThumbnailUrl(listingId, photoId),
                    Width = processed.Width,
                    Height = processed.Height,
                    Kind = kind,
                    SortOrder = startSortOrder + index,
                    IsCover = !hasCover && index == 0
                });
            }
        }
        catch
        {
            Delete(listingId, writtenPhotoIds);
            throw;
        }

        return new ListingPhotoWriteResult(true, photos, writtenPhotoIds);
    }

    public void Delete(Guid listingId, IEnumerable<Guid> photoIds)
    {
        foreach (var photoId in photoIds)
        {
            photoStorage.Delete(listingId, photoId);
        }
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
