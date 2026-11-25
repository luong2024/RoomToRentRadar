namespace RoomToRentRadar.Models.RoomDetails;

public class SimilarRoomModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public double AreaSquareMeters { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string Address { get; set; } = string.Empty;
    public string RoomType { get; set; } = "Private room";
    public bool IsVerified { get; set; } = true;

    public string FormattedPrice => $"₫{Price:N0}/month";
    public string FormattedArea => $"{AreaSquareMeters:0.#} m²";
}
