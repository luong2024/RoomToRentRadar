using RoomToRentRadar.Core.Constants;

namespace RoomToRentRadar.Features.ListingManagement.Models;

/// <summary>
/// Turns the relative photo URLs returned by the API into absolute URIs that an
/// <c>Image</c> control can load.
/// </summary>
public static class ListingPhotoUrl
{
    public static string? Resolve(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var trimmed = url.Trim();

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.AbsoluteUri;
        }

        if (!Uri.TryCreate(ApiConstants.BaseAddress, UriKind.Absolute, out var baseAddress))
            return null;

        return Uri.TryCreate(baseAddress, trimmed.TrimStart('/'), out var combined)
            ? combined.AbsoluteUri
            : null;
    }
}
