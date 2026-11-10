using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadar.Application.Mappers;

public static class RoomMapper
{
    public static RoomCardDto ToCardDto(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);

        return new RoomCardDto(
            Id: room.Id,
            Title: room.Title,
            MonthlyRent: room.MonthlyRent,
            AreaSquareMeters: room.AreaSquareMeters,
            DistanceToCampusKm: room.DistanceToCampusKm,
            IsVerified: room.IsVerified,
            ThumbnailUrl: room.PhotoUrls.FirstOrDefault() ?? string.Empty,
            AddressSummary: room.FullAddress
        );
    }

    public static RoomDetailDto ToDetailDto(
        Room room,
        string? landlordPhoneNumber,
        string? unavailableMessage,
        IReadOnlyList<RoomCardDto> similarRooms)
    {
        ArgumentNullException.ThrowIfNull(room);

        return new RoomDetailDto(
            Id: room.Id,
            Title: room.Title,
            Description: room.Description,
            MonthlyRent: room.MonthlyRent,
            DepositAmount: room.DepositAmount,
            AreaSquareMeters: room.AreaSquareMeters,
            RoomType: room.RoomType,
            Amenities: room.Amenities ?? Array.Empty<string>(),
            FullAddress: room.FullAddress,
            LandlordName: room.LandlordName,
            LandlordPhoneNumber: landlordPhoneNumber,
            PhotoUrls: room.PhotoUrls ?? Array.Empty<string>(),
            Panorama360Url: room.Panorama360Url,
            HouseRules: room.HouseRules ?? Array.Empty<string>(),
            Status: room.Status.ToString(),
            LastUpdatedAt: room.LastUpdatedAt,
            IsAvailable: room.IsAvailable,
            UnavailableMessage: unavailableMessage,
            SimilarRooms: similarRooms ?? Array.Empty<RoomCardDto>()
        );
    }
}
