namespace RoomToRentRadar.Domain.Entities;

public sealed class ListingHouseRuleVersion
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }
    public long Version { get; set; }
    public string QuietHours { get; set; } = string.Empty;
    public string GuestPolicy { get; set; } = string.Empty;
    public string CookingRules { get; set; } = string.Empty;
    public string ParkingRules { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
