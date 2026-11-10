namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

public record RoomDetailDto(
    string Id,
    string Title,
    string Description,
    decimal MonthlyRent,
    decimal DepositAmount,
    double AreaSquareMeters,
    string RoomType,
    IReadOnlyList<string> Amenities,
    string FullAddress,
    string LandlordName,
    string? LandlordPhoneNumber,
    IReadOnlyList<string> PhotoUrls,
    string? Panorama360Url,
    IReadOnlyList<string> HouseRules,
    string Status,
    DateTime LastUpdatedAt,
    bool IsAvailable,
    string? UnavailableMessage,
    IReadOnlyList<RoomCardDto> SimilarRooms
);
