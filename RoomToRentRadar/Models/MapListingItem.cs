using System.Globalization;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Models;

public sealed record MapListingItem(ListingSummaryDto Listing, double DistanceKm)
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public string Title => Listing.Title;

    public string RentText => $"{Listing.Price.ToString("N0", Vietnamese)} đ/tháng";

    public string MapPriceText => Listing.Price >= 1_000_000
        ? $"{(Listing.Price / 1_000_000d).ToString("0.#", Vietnamese)} tr"
        : Listing.Price >= 1_000 ? $"{(Listing.Price / 1_000d).ToString("0.#", Vietnamese)}k"
        : $"{Listing.Price.ToString("N0", Vietnamese)} đ";

    public string AreaText => $"{Listing.AreaM2.ToString("0.#", Vietnamese)} m²";

    public string PhotoSource => Uri.TryCreate(Listing.CoverPhotoUrl, UriKind.Absolute, out var photo)
        && photo.Scheme == Uri.UriSchemeHttps
            ? photo.AbsoluteUri
            : $"room_{Listing.Id.ToByteArray()[0] % 3 + 1}.png";

    public string BedroomText => $"{Listing.Bedrooms} phòng ngủ";

    public string AddressText => Listing.Address;

    public string DistanceText => DistanceKm < 1
        ? $"{(DistanceKm * 1000).ToString("0", Vietnamese)} m từ tâm khu vực"
        : $"{DistanceKm.ToString("0.00", Vietnamese)} km từ tâm khu vực";
}
