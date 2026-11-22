using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Models.Listings;
using RoomToRentRadar.Ports.Models.Photos;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Ports.Outbound.Services;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.UseCases.Listings;

public sealed class ListingManagementService(
    IListingManagementRepository repository,
    IUnitOfWork unitOfWork,
    IPhotoStorage photoStorage,
    IPhotoProcessor photoProcessor) : IListingManagementUseCase
{
    public async Task<IReadOnlyList<ManagedListingDto>> GetMineAsync(
        Guid ownerId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var listings = await repository.GetMineAsync(ownerId, limit, offset, cancellationToken);
        return listings.Select(ToManaged).ToList();
    }

    public async Task<ListingWriteResult<ManagedListingDto>> CreateAsync(
        Guid ownerId,
        string defaultLandlordName,
        ListingWriteCommand command,
        CancellationToken cancellationToken)
    {
        var listingId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var write = await WritePhotosAsync(
            listingId,
            command.Photos,
            startSortOrder: 0,
            hasCover: false,
            cancellationToken);
        if (!write.Succeeded)
        {
            return ListingWriteResult<ManagedListingDto>.Failure(ListingWriteErrorCodes.InvalidPhoto);
        }

        var listing = new Listing
        {
            Id = listingId,
            Title = command.Title,
            Description = command.Description ?? string.Empty,
            Price = command.Price,
            Deposit = command.Deposit,
            AreaM2 = command.AreaM2,
            RoomType = command.RoomType,
            Bedrooms = command.Bedrooms ?? 0,
            Address = command.Address,
            Lat = command.Lat ?? 0,
            Lng = command.Lng ?? 0,
            Status = ListingStatus.PendingReview,
            CreatedAt = now,
            UpdatedAt = now,
            OwnerId = ownerId,
            LandlordName = command.LandlordName ?? defaultLandlordName,
            LandlordPhone = command.LandlordPhone ?? string.Empty,
            Photos = write.Photos.ToList()
        };

        try
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            repository.Add(listing);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ListingWriteResult<ManagedListingDto>.Success(ToManaged(listing));
        }
        catch
        {
            DeleteWritten(listingId, write.WrittenPhotoIds);
            throw;
        }
    }

    public async Task<ListingWriteResult<ManagedListingDto>> UpdateAsync(
        Guid ownerId,
        Guid listingId,
        ListingWriteCommand command,
        CancellationToken cancellationToken)
    {
        var listing = await repository.FindOwnedAsync(ownerId, listingId, cancellationToken);

        if (listing is null)
        {
            return ListingWriteResult<ManagedListingDto>.Failure(ListingWriteErrorCodes.NotFound);
        }

        if (listing.Photos.Count + command.Photos.Count > PhotoUploadLimits.MaxPhotosPerListing)
        {
            return ListingWriteResult<ManagedListingDto>.Failure(
                ListingWriteErrorCodes.PhotoLimitExceeded);
        }

        var hasCover = listing.Photos.Any(photo => photo.IsCover);
        var nextSortOrder = listing.Photos.Count == 0 ? 0 : listing.Photos.Max(photo => photo.SortOrder) + 1;

        var write = await WritePhotosAsync(
            listingId,
            command.Photos,
            nextSortOrder,
            hasCover,
            cancellationToken);
        if (!write.Succeeded)
        {
            return ListingWriteResult<ManagedListingDto>.Failure(ListingWriteErrorCodes.InvalidPhoto);
        }

        foreach (var photo in write.Photos)
        {
            listing.Photos.Add(photo);
        }

        ApplyDetails(listing, command);

        try
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            if (write.Photos.Count > 0)
            {
                // Keys are client-generated, so the photos must be marked Added
                // explicitly; a tracked-navigation attach would treat them as Modified.
                repository.AddPhotoRange(write.Photos);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ListingWriteResult<ManagedListingDto>.Success(ToManaged(listing));
        }
        catch
        {
            DeleteWritten(listingId, write.WrittenPhotoIds);
            throw;
        }
    }

    public async Task<ListingWriteResult<ManagedListingDto>> UnpublishAsync(
        Guid ownerId,
        Guid listingId,
        CancellationToken cancellationToken)
    {
        var listing = await repository.FindOwnedAsync(ownerId, listingId, cancellationToken);

        if (listing is null)
        {
            return ListingWriteResult<ManagedListingDto>.Failure(ListingWriteErrorCodes.NotFound);
        }

        if (listing.Status == ListingStatus.Removed)
        {
            return ListingWriteResult<ManagedListingDto>.Failure(
                ListingWriteErrorCodes.InvalidListingTransition);
        }

        if (listing.Status != ListingStatus.Hidden)
        {
            listing.Status = ListingStatus.Hidden;
            listing.UpdatedAt = DateTime.UtcNow;
            listing.Version++;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ListingWriteResult<ManagedListingDto>.Success(ToManaged(listing));
    }

    public async Task<ManagedListingDto?> GetManagedAsync(
        Guid ownerId,
        Guid listingId,
        CancellationToken cancellationToken)
    {
        var listing = await repository.GetManagedAsync(ownerId, listingId, cancellationToken);
        return listing is null ? null : ToManaged(listing);
    }

    private sealed record PhotoWriteResult(
        bool Succeeded,
        IReadOnlyList<ListingPhoto> Photos,
        IReadOnlyList<Guid> WrittenPhotoIds);

    private async Task<PhotoWriteResult> WritePhotosAsync(
        Guid listingId,
        IReadOnlyList<PhotoUpload> files,
        int startSortOrder,
        bool hasCover,
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
                    DeleteWritten(listingId, writtenPhotoIds);
                    return new PhotoWriteResult(false, [], []);
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
                    Kind = ListingPhotoKind.Photo,
                    SortOrder = startSortOrder + index,
                    IsCover = !hasCover && index == 0
                });
            }
        }
        catch
        {
            DeleteWritten(listingId, writtenPhotoIds);
            throw;
        }

        return new PhotoWriteResult(true, photos, writtenPhotoIds);
    }

    private void DeleteWritten(Guid listingId, IReadOnlyList<Guid> writtenPhotoIds)
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

    private static void ApplyDetails(Listing listing, ListingWriteCommand command)
    {
        listing.Title = command.Title;
        listing.Address = command.Address;
        listing.Price = command.Price;
        listing.Deposit = command.Deposit;
        listing.AreaM2 = command.AreaM2;
        listing.RoomType = command.RoomType;
        listing.Description = command.Description ?? listing.Description;
        listing.Bedrooms = command.Bedrooms ?? listing.Bedrooms;
        listing.Lat = command.Lat ?? listing.Lat;
        listing.Lng = command.Lng ?? listing.Lng;
        listing.LandlordName = command.LandlordName ?? listing.LandlordName;
        listing.LandlordPhone = command.LandlordPhone ?? listing.LandlordPhone;
        listing.UpdatedAt = DateTime.UtcNow;
        listing.Version++;

        // Edited published content has to pass moderation again before it is public.
        if (listing.Status == ListingStatus.Published)
        {
            listing.Status = ListingStatus.PendingReview;
        }
    }

    private static ManagedListingDto ToManaged(Listing listing) => new(
        listing.Id,
        listing.Title,
        listing.Description,
        listing.Price,
        listing.Deposit,
        listing.AreaM2,
        listing.RoomType,
        listing.Bedrooms,
        listing.Address,
        listing.Lat,
        listing.Lng,
        listing.Status,
        listing.CreatedAt,
        listing.UpdatedAt,
        listing.Amenities
            .OrderBy(amenity => amenity.SortOrder)
            .ThenBy(amenity => amenity.Id)
            .Select(amenity => amenity.Code)
            .ToList(),
        listing.Rules
            .OrderBy(rule => rule.SortOrder)
            .ThenBy(rule => rule.Id)
            .Select(rule => rule.Text)
            .ToList(),
        listing.LandlordName,
        listing.Photos
            .OrderByDescending(photo => photo.IsCover)
            .ThenBy(photo => photo.SortOrder)
            .ThenBy(photo => photo.Id)
            .Select(photo => new ListingPhotoDto(
                photo.Id,
                photo.ListingId,
                photo.Url,
                photo.ThumbUrl,
                photo.Width,
                photo.Height,
                photo.Kind,
                photo.Alt,
                photo.SortOrder,
                photo.IsCover))
            .ToList(),
        listing.HouseRules is null
            ? null
            : new HouseRulesDto(
                listing.HouseRules.QuietHours,
                listing.HouseRules.GuestPolicy,
                listing.HouseRules.CookingRules,
                listing.HouseRules.ParkingRules,
                listing.HouseRules.Version,
                listing.HouseRules.UpdatedAt));
}
