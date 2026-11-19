namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>HouseRulesFields</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record HouseRulesFields(
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
    string? OtherRules
);