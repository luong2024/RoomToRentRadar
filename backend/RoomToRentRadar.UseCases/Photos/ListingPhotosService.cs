using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Models.Common;
using RoomToRentRadar.Ports.Models.Photos;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Ports.Outbound.Services;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.UseCases.Photos;

public sealed class ListingPhotosService(
    IListingPhotoRepository photoRepository,
    IPhotoStorage photoStorage,
    IPhotoProcessor photoProcessor,
    IUnitOfWork unitOfWork) : IListingPhotosUseCase
{
    public async Task<UseCaseResult<IReadOnlyList<ListingPhotoDto>>> UploadAsync(
        Guid listingId,
        Guid ownerId,
        ListingPhotoKind kind,
        IReadOnlyList<PhotoUpload> files,
        CancellationToken cancellationToken)
    {
        var listing = await photoRepository.FindListingAsync(listingId, cancellationToken);
        if (listing is null || listing.OwnerId != ownerId)
        {
            return Failure(PhotoErrorCodes.ListingNotFound, $"Listing '{listingId}' does not exist.");
        }

        var existing = await photoRepository.GetSlotsAsync(listingId, cancellationToken);
        if (existing.Count + files.Count > PhotoUploadLimits.MaxPhotosPerListing)
        {
            return Failure(
                PhotoErrorCodes.PhotoLimitExceeded,
                $"A listing can have at most {PhotoUploadLimits.MaxPhotosPerListing} photos.");
        }

        var nextSortOrder = existing.Count == 0 ? 0 : existing.Max(slot => slot.SortOrder) + 1;
        var hasCover = existing.Any(slot => slot.IsCover);

        var photos = new List<ListingPhoto>(files.Count);
        var writtenPhotoIds = new List<Guid>(files.Count);

        try
        {
            for (var index = 0; index < files.Count; index++)
            {
                var content = await ReadAsync(files[index], cancellationToken);
                if (!photoProcessor.TryProcess(content, out var processed))
                {
                    DeleteWritten(listingId, writtenPhotoIds);
                    return Failure(
                        PhotoErrorCodes.InvalidPhoto,
                        "Every file must be a single-frame JPEG or PNG image within the pixel limit.");
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
                    SortOrder = nextSortOrder + index,
                    IsCover = !hasCover && index == 0
                });
            }
        }
        catch
        {
            DeleteWritten(listingId, writtenPhotoIds);
            throw;
        }

        try
        {
            photoRepository.AddRange(photos);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            DeleteWritten(listingId, writtenPhotoIds);
            throw;
        }

        return UseCaseResult<IReadOnlyList<ListingPhotoDto>>.Success(photos.ConvertAll(ToDto));
    }

    public async Task<UseCaseResult<IReadOnlyList<ListingPhotoDto>>> GetGalleryAsync(
        Guid listingId,
        Guid? viewerId,
        CancellationToken cancellationToken)
    {
        var listing = await photoRepository.FindListingAsync(listingId, cancellationToken);
        if (listing is null)
        {
            return Failure(PhotoErrorCodes.ListingNotFound, $"Listing '{listingId}' does not exist.");
        }

        if (listing.Status != ListingStatus.Published
            && (viewerId is null || listing.OwnerId != viewerId))
        {
            return Failure(PhotoErrorCodes.ListingUnavailable, "The listing is not available.");
        }

        var photos = await photoRepository.GetGalleryAsync(listingId, cancellationToken);
        return UseCaseResult<IReadOnlyList<ListingPhotoDto>>.Success(photos);
    }

    private void DeleteWritten(Guid listingId, List<Guid> writtenPhotoIds)
    {
        foreach (var photoId in writtenPhotoIds)
        {
            photoStorage.Delete(listingId, photoId);
        }
    }

    private static async Task<byte[]> ReadAsync(PhotoUpload file, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static UseCaseResult<IReadOnlyList<ListingPhotoDto>> Failure(string code, string message)
        => UseCaseResult<IReadOnlyList<ListingPhotoDto>>.Failure(new ApplicationError(code, message));

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
