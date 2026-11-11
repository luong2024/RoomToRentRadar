namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

public sealed record RoomCardDto(
    string Id,
    string Title,
    string Description,
    decimal Price,
    double Area,
    string Address,
    double Latitude,
    double Longitude,
    double DistanceKm,
    bool IsAvailable,
    bool IsVerified,
    DateTime CreatedAtUtc
);
