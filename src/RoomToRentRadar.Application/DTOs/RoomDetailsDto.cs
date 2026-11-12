namespace RoomToRentRadar.Application.DTOs;

public class RoomDetailsDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public decimal Deposit { get; set; }
    public string FormattedDeposit { get; set; } = string.Empty;
    public double Area { get; set; }
    public string FormattedArea { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public List<string> Amenities { get; set; } = [];
    public List<string> HouseRules { get; set; } = [];
    public string LandlordPhone { get; set; } = string.Empty;
    public DateTime LastUpdatedDate { get; set; }
    public bool IsVerified { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    public string? UnavailableMessage { get; set; }
}
