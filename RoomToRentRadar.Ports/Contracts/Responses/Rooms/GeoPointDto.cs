namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// Geographic coordinates DTO per docs/api/openapi.yaml schema GeoPointDto.
/// </summary>
public sealed record GeoPointDto(
    double Latitude,
    double Longitude
);
