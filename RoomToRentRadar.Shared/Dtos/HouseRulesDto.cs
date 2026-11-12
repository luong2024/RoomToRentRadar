namespace RoomToRentRadar.Shared.Dtos;

public sealed record HouseRulesDto(
    int Version,
    DateTimeOffset UpdatedAt,
    string QuietHours,
    string GuestPolicy,
    string CookingRules,
    string ParkingRules);
