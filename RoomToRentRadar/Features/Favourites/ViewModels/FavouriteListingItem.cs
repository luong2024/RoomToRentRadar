using System.Globalization;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadar.Features.Favourites.ViewModels;

public sealed record FavouriteListingItem(
    Guid ListingId,
    string Title,
    string PriceText,
    string MetaText,
    string AvailabilityText,
    bool IsAvailable,
    string Address,
    string Illustration)
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public string? CoverPhotoUrl { get; init; }

    public string PhotoSource => ResolvePhotoSource() ?? Illustration;

    private string? ResolvePhotoSource()
    {
        if (string.IsNullOrWhiteSpace(CoverPhotoUrl))
        {
            return null;
        }

        if (Uri.TryCreate(CoverPhotoUrl, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.AbsoluteUri;
        }

        // The API emits relative paths ("/media/..."); resolve them against the API host.
        return Uri.TryCreate(new Uri(ApiConstants.BaseAddress), CoverPhotoUrl, out var resolved)
            && (resolved.Scheme == Uri.UriSchemeHttp || resolved.Scheme == Uri.UriSchemeHttps)
            ? resolved.AbsoluteUri
            : null;
    }

    public static FavouriteListingItem FromDto(FavouriteListingDto favourite)
        => new(
            favourite.ListingId,
            favourite.Title,
            $"{favourite.Price.ToString("N0", Vietnamese)} đ",
            favourite.Bedrooms > 0
                ? $"{favourite.AreaM2.ToString("N0", Vietnamese)} m² · {favourite.Bedrooms} PN"
                : $"{favourite.AreaM2.ToString("N0", Vietnamese)} m²",
            favourite.IsAvailable ? "● Còn trống" : "● Không còn khả dụng",
            favourite.IsAvailable,
            favourite.Address,
            $"room_{favourite.ListingId.ToByteArray()[0] % 3 + 1}.png")
        {
            CoverPhotoUrl = favourite.CoverPhotoUrl
        };
}
