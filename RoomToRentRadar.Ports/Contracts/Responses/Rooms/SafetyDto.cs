namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>SafetyDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record SafetyDto(
    int Score,
    bool IsVerified,
    IReadOnlyList<SafetyFeature> Features
);