namespace RoomToRentRadar.Domain.Entities;

public sealed class Room
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public double Area { get; init; }
    public string Address { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string LandlordId { get; init; } = string.Empty;
    public bool IsAvailable { get; init; } = true;
    public bool IsVerified { get; init; } = true;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
