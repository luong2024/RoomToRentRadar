using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Domain;

public sealed class Listing
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long Price { get; set; }
    public double AreaM2 { get; set; }
    public int Bedrooms { get; set; }
    public RoomType RoomType { get; set; }
    public string Address { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public ListingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? OwnerId { get; set; }
    public ICollection<ListingPhoto> Photos { get; set; } = [];
    public ICollection<ListingAmenity> Amenities { get; set; } = [];
    public ICollection<ListingRule> Rules { get; set; } = [];
}
