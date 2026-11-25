namespace RoomToRentRadar.Domain.Entities;

public enum RoomStatus
{
    Available = 0,
    Rented = 1,
    Pending = 2,
    UnderMaintenance = 3
}

public class Room
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public double Area { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public List<string> ImageUrls { get; set; } = [];
    public bool IsVerified { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public decimal? Deposit { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public List<string> Amenities { get; set; } = [];
    public List<string> HouseRules { get; set; } = [];
    public Guid? LandlordId { get; set; }
    public string LandlordPhone { get; set; } = string.Empty;
    public DateTime? LastUpdatedDate { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
