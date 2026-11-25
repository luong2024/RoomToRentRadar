using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.UseCases.Favourites;

internal static class FavouriteRoomMapper
{
    public static FavouriteRoomItemDto ToDto(FavouriteRoom favourite, Room? room)
    {
        ArgumentNullException.ThrowIfNull(favourite);

        if (room is null)
        {
            return new FavouriteRoomItemDto
            {
                RoomId = favourite.RoomId,
                Id = favourite.RoomId,
                Title = "Room no longer exists",
                Description = string.Empty,
                Price = 0m,
                FormattedPrice = "0 VND",
                Deposit = null,
                FormattedDeposit = null,
                Area = 0d,
                AreaSqm = 0d,
                FormattedArea = "0 m²",
                Address = string.Empty,
                RoomType = string.Empty,
                Bedrooms = null,
                Bathrooms = null,
                Amenities = [],
                ImageUrls = [],
                ThumbnailUrl = null,
                Latitude = null,
                Longitude = null,
                IsVerified = false,
                Status = "Deleted",
                IsAvailable = false,
                IsDeleted = true,
                UnavailableMessage = "This room listing has been removed or is no longer available.",
                LastUpdatedDate = null,
                FavouritedAtUtc = favourite.CreatedAtUtc
            };
        }

        var isAvailable = room.IsDisplayable;
        var location = isAvailable ? room.GetLocation() : null;
        var imageUrls = (room.ImageUrls ?? [])
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .ToList();
        var thumbnailUrl = !string.IsNullOrWhiteSpace(room.ThumbnailUrl)
            ? room.ThumbnailUrl.Trim()
            : imageUrls.FirstOrDefault();

        return new FavouriteRoomItemDto
        {
            RoomId = room.Id,
            Id = room.Id,
            Title = room.Title,
            Description = room.Description,
            Price = room.Price,
            FormattedPrice = $"{room.Price:N0} VND",
            Deposit = room.Deposit,
            FormattedDeposit = room.Deposit.HasValue ? $"{room.Deposit.Value:N0} VND" : null,
            Area = room.Area,
            AreaSqm = room.Area,
            FormattedArea = $"{room.Area} m²",
            Address = room.Address ?? string.Empty,
            RoomType = room.RoomType ?? string.Empty,
            Bedrooms = room.Bedrooms,
            Bathrooms = room.Bathrooms,
            Amenities = room.Amenities ?? [],
            ImageUrls = imageUrls,
            ThumbnailUrl = thumbnailUrl,
            Latitude = location?.Latitude,
            Longitude = location?.Longitude,
            IsVerified = room.IsVerified,
            Status = room.Status.ToString(),
            IsAvailable = isAvailable,
            IsDeleted = false,
            UnavailableMessage = isAvailable ? null : "This room is currently unavailable.",
            LastUpdatedDate = room.LastUpdatedDate,
            FavouritedAtUtc = favourite.CreatedAtUtc
        };
    }
}
