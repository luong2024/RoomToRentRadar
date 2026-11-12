namespace RoomToRentRadar.Shared.Dtos;

public sealed class ListingCardDto
{
    public string Id { get; set; } = string.Empty;

    public string CampusId { get; set; } = "ictu-main";

    public string Title { get; set; } = string.Empty;

    public decimal Rent { get; set; }

    public double Area { get; set; }

    public double DistanceMeters { get; set; }

    public string RoomType { get; set; } = "Studio";

    public string Address { get; set; } = string.Empty;

    public string ThumbnailUrl { get; set; } = string.Empty;

    public string Status { get; set; } = "Available";

    public bool IsVerified { get; set; }
}
