namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>Matches <c>GeoPointDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record GeoPointDto(
    double Latitude,
    double Longitude
);