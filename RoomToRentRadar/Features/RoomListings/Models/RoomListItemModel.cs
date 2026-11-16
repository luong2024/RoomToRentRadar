using System.Globalization;

namespace RoomToRentRadar.Features.RoomListings.Models;

/// <summary>
/// Presentation model for a single room card in the room listings screen.
/// </summary>
public sealed class RoomListItemModel
{
    private static readonly CultureInfo EnCulture = new("en-US");

    public required string Id { get; init; }

    public required string Title { get; init; }

    public decimal Price { get; init; }

    public double AreaSqm { get; init; }

    public required string Address { get; init; }

    public string District { get; init; } = "Thai Nguyen City";

    public double DistanceToCampusKm { get; init; }

    public string? ThumbnailUrl { get; init; }

    public bool IsVerified { get; init; }

    public bool IsAvailable { get; init; } = true;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<string> Amenities { get; init; } = [];

    public string FormattedPrice => string.Format(EnCulture, "₫{0:N0}/month", Price);

    public string FormattedArea => string.Format(EnCulture, "{0:0.#} m²", AreaSqm);

    public string FormattedDistance => string.Format(EnCulture, "{0:0.0} km to ICTU", DistanceToCampusKm);

    public string PrimaryAmenitiesText =>
        Amenities.Count > 0
            ? string.Join(" • ", Amenities.Take(3))
            : "Basic amenities";

    public IReadOnlyList<string> PreviewAmenities =>
        Amenities.Count > 0
            ? Amenities.Take(3).ToList()
            : ["Basic amenities"];
}
