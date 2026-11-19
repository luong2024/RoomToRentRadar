namespace RoomToRentRadar.Shared.Listings;

/// <summary>Body of <c>PUT /api/v1/listings/{id}/house-rules</c>.</summary>
public sealed class UpdateHouseRulesRequest
{
    public string? QuietHours { get; init; }
    public string? GuestPolicy { get; init; }
    public string? CookingRules { get; init; }
    public string? ParkingRules { get; init; }
}
