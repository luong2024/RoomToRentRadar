using System.Globalization;
using RoomToRentRadar.Models.MapSearch;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Models.RoomListings;

internal static class ListingLocation
{
    internal static bool IsValid(GeoCoordinate? coordinate) => coordinate is
    {
        Latitude: >= -90 and <= 90,
        Longitude: >= -180 and <= 180
    };

    internal static string Describe(GeoCoordinate? coordinate) => coordinate is null
        ? AppResources.GetString("ListingLocation_Empty")
        : string.Format(CultureInfo.CurrentCulture, AppResources.GetString("ListingLocation_Coordinates"),
            coordinate.Latitude, coordinate.Longitude);
}

internal enum ListingLocationPickerOutcome
{
    Cancelled,
    Confirmed,
    Failed
}

internal readonly record struct ListingLocationPickerResult(
    ListingLocationPickerOutcome Outcome,
    GeoCoordinate? Location = null);
