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
                Deposit = 0m,
                FormattedDeposit = "0 VND",
                Area = 0d,
                AreaSqm = 0d,
                FormattedArea = "0 m²",
                Address = string.Empty,
                RoomType = string.Empty,
                Amenities = [],
                ThumbnailUrl = null,
                Latitude = null,
                Longitude = null,
                IsVerified = false,
                Status = "Deleted",
                IsAvailable = false,
                IsDeleted = true,
                UnavailableMessage = "This room listing has been removed or is no longer available.",
                LastUpdatedDate = favourite.CreatedAtUtc.UtcDateTime,
                FavouritedAtUtc = favourite.CreatedAtUtc
            };
        }

        // Strict fail-closed availability check: must be both Available and Verified
        var isAvailable = room.Status == RoomStatus.Available && room.IsVerified;

        return new FavouriteRoomItemDto
        {
            RoomId = room.Id,
            Id = room.Id,
            Title = room.Title,
            Description = room.Description,
            Price = room.Price,
            FormattedPrice = $"{room.Price:N0} VND",
            Deposit = room.Deposit,
            FormattedDeposit = $"{room.Deposit:N0} VND",
            Area = room.Area,
            AreaSqm = room.Area,
            FormattedArea = $"{room.Area} m²",
            Address = room.Address ?? string.Empty,
            RoomType = room.RoomType ?? string.Empty,
            Amenities = room.Amenities ?? [],
            ThumbnailUrl = room.ThumbnailUrl,
            Latitude = room.Latitude,
            Longitude = room.Longitude,
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
