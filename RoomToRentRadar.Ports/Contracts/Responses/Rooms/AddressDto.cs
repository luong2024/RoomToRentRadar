namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>Matches <c>AddressDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record AddressDto(
    string AddressLine,
    string Ward,
    string District
);