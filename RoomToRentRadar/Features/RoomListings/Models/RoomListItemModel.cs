using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RoomToRentRadar.Features.RoomListings.Models;

/// <summary>
/// Presentation model for a single room card in the room listings screen.
/// </summary>
public sealed partial class RoomListItemModel : ObservableObject
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public decimal Price { get; init; }

    public double AreaSqm { get; init; }

    public required string Address { get; init; }

    public string RoomType { get; init; } = "private";

    public string District { get; init; } = "Thai Nguyen City";

    public double DistanceToCampusKm { get; init; }

    public string? ThumbnailUrl { get; init; }

    public bool IsVerified { get; init; }

    public bool IsAvailable { get; init; } = true;

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    public IReadOnlyList<string> Amenities { get; init; } = [];

    public string FormattedPrice => string.Format(CultureInfo.InvariantCulture, "₫{0:N0}/month", Price);

    public string FormattedArea => string.Format(CultureInfo.InvariantCulture, "{0:0.#} m²", AreaSqm);

    public string FormattedDistance => string.Format(CultureInfo.InvariantCulture, "{0:0.0} km to ICTU", DistanceToCampusKm);

    public string AvailabilityText => IsAvailable ? "Available" : "Rented";

    public string PrimaryAmenitiesText =>
        Amenities.Count > 0
            ? string.Join(" • ", Amenities.Take(3))
            : "Basic amenities";

    public IReadOnlyList<string> PreviewAmenities =>
        Amenities.Count > 0
            ? Amenities.Take(3).ToList()
            : ["Basic amenities"];

    [RelayCommand]
    public void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
    }
}
