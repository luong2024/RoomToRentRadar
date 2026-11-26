using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Services.Mapping;
using RoomToRentRadar.Shared.Dtos;
using SharedPhotoDto = RoomToRentRadar.Shared.Photos.PhotoDto;

namespace RoomToRentRadar.Api.Services.Landlord;

/// <summary>Map <see cref="ListingDocument"/> → <see cref="LandlordListingDto"/> (tách khỏi service đăng tin).</summary>
public static class LandlordListingMapper
{
    public static LandlordListingDto ToDto(ListingDocument listing)
    {
        var photos = listing.Photos.OrderBy(p => p.Order).Select(ToPhotoDto).ToList();

        return new LandlordListingDto
        {
            Id = listing.Id,
            CampusId = listing.CampusId,
            Title = listing.Title,
            Description = listing.Description,
            Status = listing.Status,
            StatusLabel = ListingStatusLabels.ToLabel(listing.Status),
            ModerationStatus = listing.ModerationStatus,
            IsVerified = listing.IsVerified,
            Rent = listing.Rent,
            Deposit = listing.Deposit,
            Area = listing.Area,
            RoomType = listing.RoomType,
            Address = listing.Address,
            Longitude = listing.Location.Coordinates.ElementAtOrDefault(0),
            Latitude = listing.Location.Coordinates.ElementAtOrDefault(1),
            DistanceMeters = listing.DistanceMeters,
            Amenities = [.. listing.Amenities],
            ContactName = listing.Landlord.Name,
            ContactPhone = listing.Landlord.Phone,
            PhotoCount = photos.Count,
            CoverPhotoUrl = listing.CoverThumbnailUrl(),
            Photos = photos,
            CreatedAt = listing.CreatedAt,
            UpdatedAt = listing.UpdatedAt
        };
    }

    private static SharedPhotoDto ToPhotoDto(PhotoDocument photo) => new()
    {
        Id = photo.Id,
        Url = photo.Url,
        ThumbnailUrl = photo.ThumbnailUrl,
        Is360 = photo.Is360,
        Order = photo.Order,
        Width = photo.Width,
        Height = photo.Height,
        SizeBytes = photo.SizeBytes
    };
}
