namespace RoomToRentRadar.Shared.Listings;

public sealed record HouseRulesDto(
    string QuietHours,
    string GuestPolicy,
    string CookingRules,
    string ParkingRules,
    long Version,
    DateTime UpdatedAt);
