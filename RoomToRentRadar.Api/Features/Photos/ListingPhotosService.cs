using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Photos;

public sealed class ListingPhotosService(
    AppDbContext dbContext,
    ListingPhotoWriter photoWriter)
{
    public async Task<PhotoResult<IReadOnlyList<ListingPhotoDto>>> UploadAsync(
        Guid listingId,
        Guid ownerId,
        ListingPhotoKind kind,
        IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Id == listingId)
            .Select(x => new { x.OwnerId })
            .SingleOrDefaultAsync(cancellationToken);

        if (listing is null || listing.OwnerId != ownerId)
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

        var write = await photoWriter.WriteAsync(
            listingId,
            files,
            nextSortOrder,
            hasCover,
            kind,
            cancellationToken);
        if (!write.Succeeded)
        {
            return Failure(PhotoProblemFactory.PhotoInvalid());
        }

        var newPhotos = write.Photos.ToList();

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            dbContext.ListingPhotos.AddRange(newPhotos);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            photoWriter.Delete(listingId, write.WrittenPhotoIds);
            throw;
        }

        return PhotoResult<IReadOnlyList<ListingPhotoDto>>.Success(
            newPhotos.ConvertAll(ToDto));
    }

    public async Task<PhotoResult<IReadOnlyList<ListingPhotoDto>>> GetGalleryAsync(
        Guid listingId,
        Guid? viewerId,
        CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Id == listingId)
            .Select(x => new { x.Status, x.OwnerId })
            .SingleOrDefaultAsync(cancellationToken);

        if (listing is null)
        {
            return Failure(PhotoProblemFactory.ListingMissing(listingId));
        }

        if (listing.Status != ListingStatus.Published
            && (viewerId is null || listing.OwnerId != viewerId))
        {
            return Failure(PhotoProblemFactory.ListingUnavailableProblem());
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
}
