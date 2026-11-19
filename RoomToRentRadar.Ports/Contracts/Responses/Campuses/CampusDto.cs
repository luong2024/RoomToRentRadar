namespace RoomToRentRadar.Ports.Contracts.Responses.Campuses;

/// <summary>Reference data for a campus, matching <c>CampusDto</c> in <c>docs/api/openapi.yaml</c> 1:1 (BR-CMP-01).</summary>
public sealed record CampusDto(
    string Id,
    string Name,
    string ShortName,
    double Latitude,
    double Longitude,
    bool IsDefault
);