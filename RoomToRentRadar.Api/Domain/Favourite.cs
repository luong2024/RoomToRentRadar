namespace RoomToRentRadar.Api.Domain;

public sealed class Favourite
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }
    public DateTime CreatedAt { get; set; }
}
