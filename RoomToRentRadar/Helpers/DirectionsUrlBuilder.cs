using System.Globalization;
using System.Net;

namespace RoomToRentRadar.Helpers;

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

        // The regular /maps/dir URL makes Google Maps redirect Android WebViews to an
        // intent:// URL (ERR_UNKNOWN_URL_SCHEME); output=embed stays inside the WebView.
        if (originLat is { } fromLat && originLng is { } fromLng)
        {
            var origin = string.Create(CultureInfo.InvariantCulture, $"{fromLat},{fromLng}");
            return $"https://maps.google.com/maps?saddr={origin}&daddr={destination}&output=embed";
        }

        return $"https://maps.google.com/maps?daddr={destination}&output=embed";
    }

    // Google only renders output=embed pages inside an iframe, so the WebView loads
    // this local page and the iframe fills the screen.
    public static string BuildPage(
        double destinationLat,
        double destinationLng,
        double? originLat = null,
        double? originLng = null)
    {
        var url = WebUtility.HtmlEncode(Build(destinationLat, destinationLng, originLat, originLng));

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <style>html,body{margin:0;padding:0;height:100%;width:100%;overflow:hidden}iframe{width:100%;height:100%;border:0}</style>
            </head>
            <body>
            <iframe src="{{url}}" allowfullscreen loading="eager"></iframe>
            </body>
            </html>
            """;
    }
}
