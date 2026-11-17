namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>HouseRulesDto</c> in <c>docs/api/openapi.yaml</c> 1:1 (HouseRulesFields + version metadata).</summary>
public sealed record HouseRulesDto(
    string? QuietHoursStart,
    string? QuietHoursEnd,
    string? CurfewTime,
    GuestPolicy GuestPolicy,
    string? GuestPolicyNote,
    CookingPolicy CookingPolicy,
    bool ParkingAvailable,
    string? ParkingRules,
    bool PetsAllowed,
    bool SmokingAllowed,
    string? OtherRules,
    int VersionNumber,
    DateTime UpdatedAtUtc
);