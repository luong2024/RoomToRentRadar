namespace RoomToRentRadar.Application.UseCases.RoomListings;

using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

public sealed class UpdateListingUseCase : IUpdateListingUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IListingImageStorageService _imageStorage;
    private readonly IRoomListingVisibilityWriter _visibilityWriter;
    private readonly IRoomListingCacheInvalidator _cacheInvalidator;

    public UpdateListingUseCase(
        IRoomRepository roomRepository,
        IListingImageStorageService imageStorage,
        IRoomListingVisibilityWriter visibilityWriter,
        IRoomListingCacheInvalidator cacheInvalidator)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _imageStorage = imageStorage ?? throw new ArgumentNullException(nameof(imageStorage));
        _visibilityWriter = visibilityWriter ?? throw new ArgumentNullException(nameof(visibilityWriter));
        _cacheInvalidator = cacheInvalidator ?? throw new ArgumentNullException(nameof(cacheInvalidator));
    }

    public async Task<ListingWriteResult> ExecuteAsync(Guid ownerId, string listingId, ListingWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (ownerId == Guid.Empty) throw new RequestValidationException("ownerId", "A valid landlord identity is required.");
        if (string.IsNullOrWhiteSpace(listingId)) throw new NotFoundException("Listing was not found.");
        ListingRequestValidator.Validate(request, requireNewPhotos: false);

        var existing = await _roomRepository.GetByIdAsync(listingId, cancellationToken);
        if (existing is null || existing.LandlordId != ownerId)
        {
            throw new NotFoundException("Listing was not found.");
        }

        var retained = request.ImageUrlsToRetain ?? existing.ImageUrls;
        if (retained.Any(url => string.IsNullOrWhiteSpace(url) || !existing.ImageUrls.Contains(url, StringComparer.Ordinal)))
        {
            throw new RequestValidationException("imageUrlsToRetain", "Only images already attached to this listing can be retained.");
        }
        var oldUrls = existing.ImageUrls.ToList();
        var finalImageCount = retained.Distinct(StringComparer.Ordinal).Count() + request.Photos.Count;
        if (finalImageCount > ListingPhotoLimits.MaxCount)
        {
            throw new RequestValidationException("photos", $"A listing cannot have more than {ListingPhotoLimits.MaxCount} photos.");
        }
        if (finalImageCount < 3)
        {
            throw new RequestValidationException("photos", "The listing must have at least three valid photos after the update.");
        }

        var storedUrls = new List<string>();
        Room updated;
        try
        {
            foreach (var photo in request.Photos)
            {
                await using var content = photo.OpenReadStream();
                storedUrls.Add(await _imageStorage.StoreAsync(ownerId, listingId, content, photo.FileName, photo.ContentType, photo.Length, cancellationToken));
            }

            var finalUrls = retained.Concat(storedUrls).Distinct(StringComparer.Ordinal).ToList();
            if (finalUrls.Count < 3) throw new RequestValidationException("photos", "The listing must have at least three valid photos after the update.");

            updated = CopyRoom(existing);
            updated.Title = request.Title!.Trim();
            updated.Address = request.Address!.Trim();
            updated.Price = request.MonthlyRentVnd;
            updated.Deposit = request.DepositVnd;
            updated.Area = request.AreaM2;
            updated.RoomType = request.RoomType!;
            updated.Description = request.Description?.Trim() ?? string.Empty;
            updated.ImageUrls = finalUrls;
            updated.ThumbnailUrl = finalUrls.FirstOrDefault();
            updated.Status = RoomStatus.Pending;
            updated.IsVerified = false;
            updated.LastUpdatedDate = DateTime.UtcNow;
            await _roomRepository.UpdateAsync(updated, cancellationToken);
        }
        catch
        {
            await ListingImageCleanup.DeleteBestEffortAsync(_imageStorage, storedUrls, cancellationToken);
            throw;
        }

        // Finish updating public visibility after the save, even if the client disconnected.
        var campusIds = await _visibilityWriter.HideAsync(updated.Id, CancellationToken.None);
        foreach (var campusId in campusIds)
        {
            await _cacheInvalidator.InvalidateCampusAsync(campusId, CancellationToken.None);
        }

        var removed = oldUrls.Except(updated.ImageUrls, StringComparer.Ordinal);
        await ListingImageCleanup.DeleteBestEffortAsync(_imageStorage, removed, CancellationToken.None);
        return new ListingWriteResult(updated.Id, updated.Status.ToString(), updated.ImageUrls);
    }

    private static Room CopyRoom(Room room) => new()
    {
        Id = room.Id,
        LandlordId = room.LandlordId,
        Title = room.Title,
        Description = room.Description,
        Price = room.Price,
        Area = room.Area,
        Address = room.Address,
        ThumbnailUrl = room.ThumbnailUrl,
        ImageUrls = room.ImageUrls.ToList(),
        IsVerified = room.IsVerified,
        Status = room.Status,
        Deposit = room.Deposit,
        RoomType = room.RoomType,
        ConcurrencyToken = room.ConcurrencyToken,
        Bedrooms = room.Bedrooms,
        Bathrooms = room.Bathrooms,
        Amenities = room.Amenities.ToList(),
        HouseRules = room.HouseRules.ToList(),
        LandlordPhone = room.LandlordPhone,
        LastUpdatedDate = room.LastUpdatedDate,
        Latitude = room.Latitude,
        Longitude = room.Longitude,
        CreatedAt = room.CreatedAt
    };
}
