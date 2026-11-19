using System.Globalization;

namespace RoomToRentRadar.Services;

public static class DirectionsUrlBuilder
{
    public static string Build(
        double destinationLat,
        double destinationLng,
        double? originLat = null,
        double? originLng = null)
    {
        var destination = string.Create(
            CultureInfo.InvariantCulture,
            $"{destinationLat},{destinationLng}");

        if (originLat is { } fromLat && originLng is { } fromLng)
        {
            var origin = string.Create(CultureInfo.InvariantCulture, $"{fromLat},{fromLng}");
            return $"https://www.google.com/maps/dir/?api=1&origin={origin}&destination={destination}";
        }

        return $"https://www.google.com/maps/dir/?api=1&destination={destination}";
    }
}
