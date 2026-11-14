namespace RoomToRentRadar.Application.DTOs;

public class SimilarRoomDto
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
