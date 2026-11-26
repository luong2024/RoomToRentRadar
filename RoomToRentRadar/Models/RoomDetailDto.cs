namespace RoomToRentRadar.Models;

public class RoomDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MonthlyRent { get; set; }
    public decimal Deposit { get; set; }
    public double Area { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Status { get; set; } = "Available"; // "Available" hoặc "Unavailable"
    public bool IsVerified { get; set; }
    public DateTime LastUpdated { get; set; }

    public string LandlordName { get; set; } = string.Empty;
    public string LandlordPhone { get; set; } = string.Empty;

    public List<string> Amenities { get; set; } = new();
    public List<string> HouseRules { get; set; } = new();
    public List<RoomSummaryDto> SimilarRooms { get; set; } = new();
}

public class RoomSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal MonthlyRent { get; set; }
    public double Area { get; set; }
    public string Address { get; set; } = string.Empty;
}