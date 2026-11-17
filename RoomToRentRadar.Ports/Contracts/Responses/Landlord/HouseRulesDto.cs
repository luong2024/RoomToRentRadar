using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Ports.Contracts.Responses.Landlord;

/// <summary>
/// Current house rules of a listing (openapi <c>HouseRulesDto</c> = <c>HouseRulesFields</c> +
/// versionNumber/updatedAtUtc, ERD §4.7).
/// </summary>
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
    DateTime UpdatedAtUtc);
