namespace RoomToRentRadar.Domain.Entities;

public sealed class ListingFavourite
{
    public Guid UserId { get; set; }
    public Guid ListingId { get; set; }
    public DateTime CreatedAt { get; set; }
}
