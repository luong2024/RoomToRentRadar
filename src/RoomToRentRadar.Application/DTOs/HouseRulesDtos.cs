namespace RoomToRentRadar.Application.DTOs;

public sealed record RoomHouseRulesDto(
    string RoomId,
    Guid? LandlordId,
    int Version,
    string QuietHours,
    string GuestPolicy,
    string CookingRules,
    string ParkingRules,
    IReadOnlyList<string> AdditionalRules,
    DateTimeOffset? UpdatedAtUtc,
    Guid? UpdatedByUserId,
    bool IsLegacyFallback);

public sealed record HouseRulesVersionDto(
    string RoomId,
    int Version,
    string QuietHours,
    string GuestPolicy,
    string CookingRules,
    string ParkingRules,
    IReadOnlyList<string> AdditionalRules,
    string? ChangeNote,
    DateTimeOffset UpdatedAtUtc,
    Guid UpdatedByUserId);

public sealed record UpdateHouseRulesCommand(
    string RoomId,
    Guid ActorUserId,
    string ActorRole,
    int? ExpectedVersion,
    string QuietHours,
    string GuestPolicy,
    string CookingRules,
    string ParkingRules,
    IReadOnlyList<string>? AdditionalRules,
    string? ChangeNote);
