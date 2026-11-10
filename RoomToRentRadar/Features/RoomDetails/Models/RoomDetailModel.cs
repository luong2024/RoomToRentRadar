namespace RoomToRentRadar.Features.RoomDetails.Models;

public class RoomDetailModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Deposit { get; set; }
    public double AreaSquareMeters { get; set; }
    public string RoomType { get; set; } = "Phòng trọ khép kín";
    public string Address { get; set; } = string.Empty;
    public ListingStatus Status { get; set; } = ListingStatus.Available;
    public DateTime LastUpdated { get; set; } = DateTime.Now;

    public string LandlordName { get; set; } = string.Empty;
    public string? LandlordPhone { get; set; }
    public string LandlordEmail { get; set; } = string.Empty;
    public string LandlordAvatar { get; set; } = string.Empty;

    public List<string> ImageUrls { get; set; } = [];
    public List<RoomAmenityModel> Amenities { get; set; } = [];
    public List<string> HouseRules { get; set; } = [];

    // Helper format
    public string FormattedPrice => $"{Price:N0} ₫/tháng";
    public string FormattedDeposit => $"{Deposit:N0} ₫";
    public string FormattedArea => $"{AreaSquareMeters} m²";
    public string FormattedLastUpdated => $"Cập nhật: {LastUpdated:dd/MM/yyyy}";
}