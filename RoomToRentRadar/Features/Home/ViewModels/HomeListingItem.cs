using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.Home.ViewModels;

public sealed partial class HomeListingItem : ObservableObject
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public HomeListingItem(
        Guid id,
        string title,
        string priceText,
        string areaText,
        string availabilityText,
        string address,
        string illustration)
    {
        Id = id;
        Title = title;
        PriceText = priceText;
        AreaText = areaText;
        AvailabilityText = availabilityText;
        Address = address;
        Illustration = illustration;
    }

    public Guid Id { get; }
    public string Title { get; }
    public string PriceText { get; }
    public string AreaText { get; }
    public string AvailabilityText { get; }
    public string Address { get; }
    public string Illustration { get; }
    public double Latitude { get; init; } = double.NaN;
    public double Longitude { get; init; } = double.NaN;
    public string? CoverPhotoUrl { get; init; }

    [ObservableProperty]
    public partial bool IsFavourite { get; set; }

    public bool HasValidCoordinates => double.IsFinite(Latitude) && double.IsFinite(Longitude)
        && Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180;

    public string PhotoSource => Uri.TryCreate(CoverPhotoUrl, UriKind.Absolute, out var photo)
        && photo.Scheme == Uri.UriSchemeHttps ? photo.AbsoluteUri : Illustration;

    public static HomeListingItem FromDto(ListingSummaryDto listing)
        => new(
            listing.Id,
            listing.Title,
            $"{listing.Price.ToString("N0", Vietnamese)} đ",
            $"{listing.AreaM2.ToString("N0", Vietnamese)} m²",
            listing.Bedrooms > 0 ? $"●  Còn trống · {listing.Bedrooms} PN" : "●  Còn trống",
            listing.Address,
            $"room_{listing.Id.ToByteArray()[0] % 3 + 1}.png")
        {
            Latitude = listing.Lat,
            Longitude = listing.Lng,
            CoverPhotoUrl = listing.CoverPhotoUrl
        };
}
