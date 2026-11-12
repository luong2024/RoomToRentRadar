using System.Globalization;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Models;

public sealed record MapListingItem(ListingSummaryDto Listing, double DistanceKm)
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public string Title => Listing.Title;

    public string RentText => $"{Listing.Price.ToString("N0", Vietnamese)} đ/tháng";

    public string AreaText => $"{Listing.AreaM2:0.#} m²";

    public string BedroomText => $"{Listing.Bedrooms} phòng ngủ";

    public string AddressText => Listing.Address;

    public string DistanceText => DistanceKm < 1
        ? $"{DistanceKm * 1000:0} m từ tâm khu vực"
        : $"{DistanceKm:0.00} km từ tâm khu vực";
}
