namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// Safety score and verification criteria per docs/api/openapi.yaml schema SafetyDto and BR-LST-08.
/// </summary>
public sealed record SafetyDto(
    int Score,
    bool IsVerified,
    IReadOnlyList<string> Features
);
