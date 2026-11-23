namespace RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;

public sealed record UpdateHouseRulesRequest(
    string QuietHours,
    string GuestPolicy,
    string CookingRules,
    string ParkingRules,
    IReadOnlyList<string>? AdditionalRules = null,
    string? ChangeNote = null,
    int? ExpectedVersion = null);
