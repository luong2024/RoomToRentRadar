using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Domain.Entities;

public class Room
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MonthlyRent { get; set; }
    public decimal DepositAmount { get; set; }
    public double AreaSquareMeters { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public IReadOnlyList<string> Amenities { get; set; } = Array.Empty<string>();
    public string FullAddress { get; set; } = string.Empty;
    public string LandlordName { get; set; } = string.Empty;
    public string LandlordPhoneNumber { get; set; } = string.Empty;
    public IReadOnlyList<string> PhotoUrls { get; set; } = Array.Empty<string>();
    public string? Panorama360Url { get; set; }
    public IReadOnlyList<string> HouseRules { get; set; } = Array.Empty<string>();
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsVerified { get; set; }
    public double DistanceToCampusKm { get; set; }

    public bool IsAvailable => Status == RoomStatus.Available;
}
