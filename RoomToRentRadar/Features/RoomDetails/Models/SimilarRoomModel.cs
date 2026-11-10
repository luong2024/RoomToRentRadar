namespace RoomToRentRadar.Features.RoomDetails.Models;

public class SimilarRoomModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public double AreaSquareMeters { get; set; }
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string FormattedPrice => $"{Price:N0} ₫/tháng";
}