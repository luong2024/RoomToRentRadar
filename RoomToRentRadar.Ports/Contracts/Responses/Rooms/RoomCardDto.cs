namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

public record RoomCardDto(
    string Id,
    string Title,
    decimal MonthlyRent,
    double AreaSquareMeters,
    double DistanceToCampusKm,
    bool IsVerified,
    string ThumbnailUrl,
    string AddressSummary
);
