namespace RoomToRentRadar.Features.RoomDetails.Models;

/// <summary>
/// Client-side DTO representing room details returned by GET api/rooms/{id}.
/// Decoupled from backend assembly references.
/// </summary>
public class RoomDetailApiDto
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
    public List<SimilarRoomApiDto> SimilarRooms { get; set; } = [];
}

/// <summary>
/// Client-side DTO for similar rooms embedded inside RoomDetailApiDto.
/// </summary>
public class SimilarRoomApiDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public double Area { get; set; }
    public string FormattedArea { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
}
