using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Photos;

public sealed class ListingPhotosService(
    AppDbContext dbContext,
    PhotoStorage photoStorage,
    PhotoProcessor photoProcessor)
{
    public async Task<PhotoResult<IReadOnlyList<ListingPhotoDto>>> UploadAsync(
        Guid listingId,
        ListingPhotoKind kind,
        IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Listings.AnyAsync(x => x.Id == listingId, cancellationToken))
        {
            return Failure(PhotoProblemFactory.ListingMissing(listingId));
        }

        var existing = await dbContext.ListingPhotos
            .Where(x => x.ListingId == listingId)
            .Select(x => new { x.SortOrder, x.IsCover })
            .ToListAsync(cancellationToken);

        if (existing.Count + files.Count > PhotoUploadLimits.MaxPhotosPerListing)
        {
            return Failure(PhotoProblemFactory.PhotoLimitReached());
        }

        var nextSortOrder = existing.Count == 0 ? 0 : existing.Max(x => x.SortOrder) + 1;
        var hasCover = existing.Exists(x => x.IsCover);

        var writtenPhotos = new List<Guid>();
        var newPhotos = new List<ListingPhoto>(files.Count);

        try
        {
            foreach (var file in files)
            {
                var content = await ReadAsync(file, cancellationToken);
                if (!photoProcessor.TryProcess(content, out var processed))
                {
                    DeleteWrittenPhotos(listingId, writtenPhotos);
                    return Failure(PhotoProblemFactory.PhotoInvalid());
                }

                var photoId = Guid.NewGuid();
                await photoStorage.SaveAsync(
                    listingId,
                    photoId,
                    processed!.Original,
                    processed.Thumbnail,
                    cancellationToken);
                writtenPhotos.Add(photoId);

                newPhotos.Add(new ListingPhoto
                {
                    Id = photoId,
                    ListingId = listingId,
                    Url = photoStorage.GetOriginalUrl(listingId, photoId),
                    ThumbUrl = photoStorage.GetThumbnailUrl(listingId, photoId),
                    Width = processed.Width,
                    Height = processed.Height,
                    Kind = kind,
                    SortOrder = nextSortOrder + newPhotos.Count,
                    IsCover = !hasCover && newPhotos.Count == 0
                });
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            dbContext.ListingPhotos.AddRange(newPhotos);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            DeleteWrittenPhotos(listingId, writtenPhotos);
            throw;
        }

        return PhotoResult<IReadOnlyList<ListingPhotoDto>>.Success(
            newPhotos.ConvertAll(ToDto));
    }

    public async Task<PhotoResult<IReadOnlyList<ListingPhotoDto>>> GetGalleryAsync(
        Guid listingId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Listings.AnyAsync(x => x.Id == listingId, cancellationToken))
        {
            return Failure(PhotoProblemFactory.ListingMissing(listingId));
        }

        var photos = await dbContext.ListingPhotos
            .AsNoTracking()
            .Where(x => x.ListingId == listingId)
            .OrderByDescending(x => x.IsCover)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new ListingPhotoDto(
                x.Id,
                x.ListingId,
                x.Url,
                x.ThumbUrl,
                x.Width,
                x.Height,
                x.Kind,
                x.Alt,
                x.SortOrder,
                x.IsCover))
            .ToListAsync(cancellationToken);

        return PhotoResult<IReadOnlyList<ListingPhotoDto>>.Success(photos);
    }

    private static PhotoResult<IReadOnlyList<ListingPhotoDto>> Failure(ProblemDetails problem)
        => PhotoResult<IReadOnlyList<ListingPhotoDto>>.Failure(problem);

    private static ListingPhotoDto ToDto(ListingPhoto photo) => new(
        photo.Id,
        photo.ListingId,
        photo.Url,
        photo.ThumbUrl,
        photo.Width,
        photo.Height,
        photo.Kind,
        photo.Alt,
        photo.SortOrder,
        photo.IsCover);

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private void DeleteWrittenPhotos(Guid listingId, List<Guid> writtenPhotos)
    {
        foreach (var photoId in writtenPhotos)
        {
            photoStorage.Delete(listingId, photoId);
        }

        writtenPhotos.Clear();
    }
}
