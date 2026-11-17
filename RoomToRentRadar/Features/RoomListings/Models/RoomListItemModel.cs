using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RoomToRentRadar.Features.RoomListings.Models;

/// <summary>
/// Presentation model for a single room card in the room listings screen.
/// </summary>
public sealed partial class RoomListItemModel : ObservableObject
{
    private static readonly CultureInfo ViCulture = new("vi-VN");

    public required string Id { get; init; }

    public required string Title { get; init; }

    public decimal Price { get; init; }

    public double AreaSqm { get; init; }

    public required string Address { get; init; }

    public string RoomType { get; init; } = "private";

    public string District { get; init; } = "TP. Thái Nguyên";

    public double DistanceToCampusKm { get; init; }

    public string? ThumbnailUrl { get; init; }

    public bool IsVerified { get; init; }

    public bool IsAvailable { get; init; } = true;

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<string> Amenities { get; init; } = [];

    public string FormattedPrice => string.Format(ViCulture, "{0:N0} đ/tháng", Price);

    public string FormattedArea => string.Format(ViCulture, "{0:0.#} m²", AreaSqm);

    public string FormattedDistance => string.Format(ViCulture, "{0:0.0} km tới ICTU", DistanceToCampusKm);

    public string AvailabilityText => IsAvailable ? "Còn trống" : "Đã thuê";

    public string PrimaryAmenitiesText =>
        Amenities.Count > 0
            ? string.Join(" • ", Amenities.Take(3))
            : "Tiện nghi cơ bản";

    public IReadOnlyList<string> PreviewAmenities =>
        Amenities.Count > 0
            ? Amenities.Take(3).ToList()
            : ["Tiện nghi cơ bản"];

    [RelayCommand]
    public void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
    }
}
