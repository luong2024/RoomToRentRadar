using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Api.Features.Photos;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

public sealed class ListingManagementService(
    AppDbContext dbContext,
    ListingPhotoWriter photoWriter)
{
    public async Task<ListingWriteResult<ManagedListingDto>> CreateAsync(
        Guid ownerId,
        string defaultLandlordName,
        ListingWriteInput input,
        CancellationToken cancellationToken)
    {
        var listingId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var write = await photoWriter.WriteAsync(
            listingId,
            input.Photos,
            startSortOrder: 0,
            hasCover: false,
            ListingPhotoKind.Photo,
            cancellationToken);
        if (!write.Succeeded)
        {
            return ListingWriteResult<ManagedListingDto>.Failure(
                ListingWriteProblemFactory.InvalidPhotoProblem());
        }

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
            LandlordPhone = input.LandlordPhone ?? string.Empty,
            Photos = write.Photos.ToList()
        };

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            dbContext.Listings.Add(listing);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ListingWriteResult<ManagedListingDto>.Success(ToManaged(listing));
        }
        catch
        {
            photoWriter.Delete(listingId, write.WrittenPhotoIds);
            throw;
        }
    }

    public async Task<ListingWriteResult<ManagedListingDto>> UpdateAsync(
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
            return ListingWriteResult<ManagedListingDto>.Failure(
                ListingWriteProblemFactory.NotFoundProblem());
        }

        if (listing.Photos.Count + input.Photos.Count > PhotoUploadLimits.MaxPhotosPerListing)
        {
            return ListingWriteResult<ManagedListingDto>.Failure(
                ListingWriteProblemFactory.PhotoLimitExceededProblem());
        }

        var hasCover = listing.Photos.Any(x => x.IsCover);
        var nextSortOrder = listing.Photos.Count == 0 ? 0 : listing.Photos.Max(x => x.SortOrder) + 1;

        var write = await photoWriter.WriteAsync(
            listingId,
            input.Photos,
            nextSortOrder,
            hasCover,
            ListingPhotoKind.Photo,
            cancellationToken);
        if (!write.Succeeded)
        {
            return ListingWriteResult<ManagedListingDto>.Failure(
                ListingWriteProblemFactory.InvalidPhotoProblem());
        }

        foreach (var photo in write.Photos)
        {
            listing.Photos.Add(photo);
        }

        ApplyDetails(listing, input);

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            if (write.Photos.Count > 0)
            {
                // Keys are client-generated, so the photos must be marked Added
                // explicitly; a tracked-navigation attach would treat them as Modified.
                dbContext.ListingPhotos.AddRange(write.Photos);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ListingWriteResult<ManagedListingDto>.Success(ToManaged(listing));
        }
        catch
        {
            photoWriter.Delete(listingId, write.WrittenPhotoIds);
            throw;
        }
    }

    public async Task<ListingWriteResult<ManagedListingDto>> UnpublishAsync(
        Guid ownerId,
        Guid listingId,
        CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .Include(x => x.Photos)
            .Include(x => x.Amenities)
            .Include(x => x.Rules)
            .SingleOrDefaultAsync(x => x.Id == listingId, cancellationToken);

        if (listing is null || listing.OwnerId != ownerId)
        {
            return ListingWriteResult<ManagedListingDto>.Failure(
                ListingWriteProblemFactory.NotFoundProblem());
        }

        if (listing.Status == ListingStatus.Removed)
        {
            return ListingWriteResult<ManagedListingDto>.Failure(
                ListingWriteProblemFactory.InvalidListingTransitionProblem());
        }

        if (listing.Status != ListingStatus.Hidden)
        {
            listing.Status = ListingStatus.Hidden;
            listing.UpdatedAt = DateTime.UtcNow;
            listing.Version++;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ListingWriteResult<ManagedListingDto>.Success(ToManaged(listing));
    }

    public async Task<ManagedListingDto?> GetManagedAsync(
        Guid ownerId,
        Guid listingId,
        CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Include(x => x.Photos)
            .Include(x => x.Amenities)
            .Include(x => x.Rules)
            .SingleOrDefaultAsync(
                x => x.Id == listingId && x.OwnerId == ownerId,
                cancellationToken);

        return listing is null ? null : ToManaged(listing);
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
            .ToList());
}
