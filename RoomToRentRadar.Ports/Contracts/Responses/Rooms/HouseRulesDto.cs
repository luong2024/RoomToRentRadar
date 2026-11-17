namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// Versioned house rules per docs/api/openapi.yaml schema HouseRulesDto and BR-HR-02.
/// </summary>
public sealed record HouseRulesDto(
    int VersionNumber,
    DateTime UpdatedAtUtc,
    string GuestPolicy = "Allowed",
    string CookingPolicy = "Allowed",
    bool ParkingAvailable = true,
    bool PetsAllowed = false,
    bool SmokingAllowed = false,
    string? QuietHoursStart = null,
    string? QuietHoursEnd = null,
    string? CurfewTime = null,
    string? GuestPolicyNote = null
);
