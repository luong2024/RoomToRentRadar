using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
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
            RoomType: room.RoomTypeDisplay,
            Amenities: room.Amenities,
            FullAddress: room.FullAddress,
            LandlordName: room.LandlordName,
            LandlordPhoneNumber: landlordPhoneNumber,
            PhotoUrls: room.PhotoUrls,
            Panorama360Url: room.Panorama360Url,
            HouseRules: room.HouseRules,
            Status: MapRoomStatus(room.Status),
            LastUpdatedAt: room.LastUpdatedAt,
            IsAvailable: room.IsAvailable,
            UnavailableMessage: unavailableMessage,
            SimilarRooms: similarRooms ?? Array.Empty<RoomCardDto>()
        );
    }

    public static string MapRoomStatus(ListingStatus status) => status switch
    {
        ListingStatus.Available => "Available",
        ListingStatus.Rented => "Rented",
        ListingStatus.Unpublished => "Unavailable",
        ListingStatus.PendingReview => "PendingVerification",
        ListingStatus.Rejected => "Unavailable",
        ListingStatus.Suspended => "Unavailable",
        _ => "Unavailable"
    };

    [Obsolete("Use ListingStatus overload")]
    public static string MapRoomStatus(RoomStatus status) => status switch
    {
        RoomStatus.Available => "Available",
        RoomStatus.Unavailable => "Unavailable",
        RoomStatus.Rented => "Rented",
        RoomStatus.PendingVerification => "PendingVerification",
        _ => "Unavailable"
    };
}
