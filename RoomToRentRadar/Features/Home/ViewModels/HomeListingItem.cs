using System.Globalization;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.Home.ViewModels;

public sealed record HomeListingItem(
    Guid Id,
    string Title,
    string PriceText,
    string AreaText,
    string AvailabilityText,
    string Address,
    string Illustration)
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public static HomeListingItem FromDto(ListingSummaryDto listing)
        => new(
            listing.Id,
            listing.Title,
            $"{listing.Price.ToString("N0", Vietnamese)} đ",
            $"{listing.AreaM2.ToString("N0", Vietnamese)} m²",
            listing.Bedrooms > 0 ? $"●  Còn trống · {listing.Bedrooms} PN" : "●  Còn trống",
            listing.Address,
            $"room_{listing.Id.ToByteArray()[0] % 3 + 1}.png");
}
