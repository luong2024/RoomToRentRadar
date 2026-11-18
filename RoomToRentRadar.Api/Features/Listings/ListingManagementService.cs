using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Api.Features.Photos;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

public sealed class ListingManagementService(
    AppDbContext dbContext,
    PhotoStorage photoStorage,
    PhotoProcessor photoProcessor)
{
    public async Task<ListingWriteResult<ListingDetailDto>> CreateAsync(
        Guid ownerId,
        string defaultLandlordName,
        ListingWriteInput input,
        CancellationToken cancellationToken)
    {
        var listingId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var listing = new Listing
        {
            Id = listingId,
            Title = input.Title,
            Description = input.Description ?? string.Empty,
            Price = input.Price,
            Deposit = input.Deposit,
            AreaM2 = input.AreaM2,
            RoomType = input.RoomType,
            Bedrooms = input.Bedrooms ?? 0,
            Address = input.Address,
            Lat = input.Lat ?? 0,
            Lng = input.Lng ?? 0,
            Status = ListingStatus.PendingReview,
            CreatedAt = now,
            UpdatedAt = now,
            OwnerId = ownerId,
            LandlordName = input.LandlordName ?? defaultLandlordName,
            LandlordPhone = input.LandlordPhone ?? string.Empty
        };

        var photos = new List<ListingPhoto>(input.Photos.Count);
        var writtenPhotos = new List<Guid>(input.Photos.Count);

        try
        {
            if (!await TryWritePhotosAsync(
                    listingId,
                    input.Photos,
                    startSortOrder: 0,
                    hasCover: false,
                    photos,
                    writtenPhotos,
                    cancellationToken))
            {
                DeleteWrittenPhotos(listingId, writtenPhotos);
                return ListingWriteResult<ListingDetailDto>.Failure(
                    ListingWriteProblemFactory.InvalidPhotoProblem());
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            dbContext.Listings.Add(listing);
            dbContext.ListingPhotos.AddRange(photos);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ListingWriteResult<ListingDetailDto>.Success(ToDetail(listing));
        }
        catch
        {
            DeleteWrittenPhotos(listingId, writtenPhotos);
            throw;
        }
    }

    public async Task<ListingWriteResult<ListingDetailDto>> UpdateAsync(
        Guid ownerId,
        Guid listingId,
        ListingWriteInput input,
        CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .Include(x => x.Photos)
            .Include(x => x.Amenities)
            .Include(x => x.Rules)
            .SingleOrDefaultAsync(x => x.Id == listingId, cancellationToken);

        if (listing is null || listing.OwnerId != ownerId)
        {
            return ListingWriteResult<ListingDetailDto>.Failure(
                ListingWriteProblemFactory.NotFoundProblem());
        }

        if (listing.Photos.Count + input.Photos.Count > PhotoUploadLimits.MaxPhotosPerListing)
        {
            return ListingWriteResult<ListingDetailDto>.Failure(
                ListingWriteProblemFactory.PhotoLimitExceededProblem());
        }

        var hasCover = listing.Photos.Any(x => x.IsCover);
        var nextSortOrder = listing.Photos.Count == 0 ? 0 : listing.Photos.Max(x => x.SortOrder) + 1;
        var photos = new List<ListingPhoto>(input.Photos.Count);
        var writtenPhotos = new List<Guid>(input.Photos.Count);

        try
        {
            if (!await TryWritePhotosAsync(
                    listingId,
                    input.Photos,
                    nextSortOrder,
                    hasCover,
                    photos,
                    writtenPhotos,
                    cancellationToken))
            {
                DeleteWrittenPhotos(listingId, writtenPhotos);
                return ListingWriteResult<ListingDetailDto>.Failure(
                    ListingWriteProblemFactory.InvalidPhotoProblem());
            }

            ApplyDetails(listing, input);

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            if (photos.Count > 0)
            {
                dbContext.ListingPhotos.AddRange(photos);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ListingWriteResult<ListingDetailDto>.Success(ToDetail(listing));
        }
        catch
        {
            DeleteWrittenPhotos(listingId, writtenPhotos);
            throw;
        }
    }

    public async Task<ListingWriteResult<ListingDetailDto>> UnpublishAsync(
        Guid ownerId,
        Guid listingId,
        CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .Include(x => x.Amenities)
            .Include(x => x.Rules)
            .SingleOrDefaultAsync(x => x.Id == listingId, cancellationToken);

        if (listing is null || listing.OwnerId != ownerId)
        {
            return ListingWriteResult<ListingDetailDto>.Failure(
                ListingWriteProblemFactory.NotFoundProblem());
        }

        if (listing.Status != ListingStatus.Hidden)
        {
            listing.Status = ListingStatus.Hidden;
            listing.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ListingWriteResult<ListingDetailDto>.Success(ToDetail(listing));
    }

    public async Task<ListingDetailDto?> GetManagedAsync(
        Guid ownerId,
        Guid listingId,
        CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Include(x => x.Amenities)
            .Include(x => x.Rules)
            .SingleOrDefaultAsync(
                x => x.Id == listingId && x.OwnerId == ownerId,
                cancellationToken);

        return listing is null ? null : ToDetail(listing);
    }

    private static void ApplyDetails(Listing listing, ListingWriteInput input)
    {
        listing.Title = input.Title;
        listing.Address = input.Address;
        listing.Price = input.Price;
        listing.Deposit = input.Deposit;
        listing.AreaM2 = input.AreaM2;
        listing.RoomType = input.RoomType;
        listing.Description = input.Description ?? listing.Description;
        listing.Bedrooms = input.Bedrooms ?? listing.Bedrooms;
        listing.Lat = input.Lat ?? listing.Lat;
        listing.Lng = input.Lng ?? listing.Lng;
        listing.LandlordName = input.LandlordName ?? listing.LandlordName;
        listing.LandlordPhone = input.LandlordPhone ?? listing.LandlordPhone;
        listing.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<bool> TryWritePhotosAsync(
        Guid listingId,
        IReadOnlyList<IFormFile> files,
        int startSortOrder,
        bool hasCover,
        List<ListingPhoto> photos,
        List<Guid> writtenPhotos,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < files.Count; index++)
        {
            var content = await ReadAsync(files[index], cancellationToken);
            if (!photoProcessor.TryProcess(content, out var processed))
            {
                return false;
            }

            var photoId = Guid.NewGuid();
            await photoStorage.SaveAsync(
                listingId,
                photoId,
                processed!.Original,
                processed.Thumbnail,
                cancellationToken);
            writtenPhotos.Add(photoId);

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

        return true;
    }

    private static ListingDetailDto ToDetail(Listing listing) => new(
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
        listing.LandlordName);

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
