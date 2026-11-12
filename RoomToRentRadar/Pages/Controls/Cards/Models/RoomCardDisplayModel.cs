namespace RoomToRentRadar.Pages.Controls.Cards.Models;

public sealed record RoomCardDisplayModel
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public decimal PricePerMonth { get; init; }
    public double AreaSquareMeters { get; init; }
    public string Address { get; init; } = string.Empty;
    public double DistanceMeters { get; init; }
    public string ThumbnailUrl { get; init; } = string.Empty;
    public bool IsVerified { get; init; } = true;
    public bool IsAvailable { get; init; } = true;
    public bool IsBookmarked { get; init; }
    public IReadOnlyList<string> Amenities { get; init; } = [];

    public string DistanceFormatted => DistanceMeters < 1000
        ? $"{DistanceMeters:F0}m from campus"
        : $"{(DistanceMeters / 1000.0):F1}km from campus";

    public string AreaFormatted => $"{AreaSquareMeters:F0} m²";
}

