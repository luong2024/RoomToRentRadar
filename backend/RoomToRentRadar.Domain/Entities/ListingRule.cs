namespace RoomToRentRadar.Domain.Entities;

public sealed class ListingRule
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }
    public string Text { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
