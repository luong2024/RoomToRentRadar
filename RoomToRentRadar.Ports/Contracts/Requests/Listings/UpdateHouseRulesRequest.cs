namespace RoomToRentRadar.Ports.Contracts.Requests.Listings;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>UpdateHouseRulesRequest</c> in <c>docs/api/openapi.yaml</c> 1:1 (a $ref alias of HouseRulesFields).</summary>
public sealed record UpdateHouseRulesRequest(
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