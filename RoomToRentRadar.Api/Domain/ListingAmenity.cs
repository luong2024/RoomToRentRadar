using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Domain;

public sealed class ListingAmenity
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }
    public AmenityCode Code { get; set; }
    public int SortOrder { get; set; }
}
