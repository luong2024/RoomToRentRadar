namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>TravelTimeItemDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record TravelTimeItemDto(
    TravelMode Mode,
    int DistanceMeters,
    int DurationSeconds,
    bool IsEstimate
);