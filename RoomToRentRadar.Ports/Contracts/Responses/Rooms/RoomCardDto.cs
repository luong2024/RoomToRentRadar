namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

public sealed record RoomCardDto
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public decimal PricePerMonth { get; init; }
    public double AreaSquareMeters { get; init; }
    public string Address { get; init; } = string.Empty;
    public double DistanceToIctuMeters { get; init; }
    public string ThumbnailUrl { get; init; } = string.Empty;
    public bool IsVerified { get; init; } = true;
    public bool IsAvailable { get; init; } = true;
    public bool IsBookmarked { get; init; }
    public IReadOnlyList<string> Amenities { get; init; } = [];

    public string DistanceFormatted => DistanceToIctuMeters < 1000
        ? $"Cách ICTU {DistanceToIctuMeters:F0}m"
        : $"Cách ICTU {(DistanceToIctuMeters / 1000.0):F1}km";

    public string AreaFormatted => $"{AreaSquareMeters:F0} m²";
}

