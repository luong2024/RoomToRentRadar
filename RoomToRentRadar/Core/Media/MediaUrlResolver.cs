using RoomToRentRadar.Core.Constants;

namespace RoomToRentRadar.Core.Media;

public static class MediaUrlResolver
{
    public static string? Resolve(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            if (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
            {
                return absolute.AbsoluteUri;
            }

            // On Unix a rooted path ("/media/...") also parses as an absolute file URI,
            // so fall through and resolve it against the API host instead.
        }

        return Uri.TryCreate(new Uri(ApiConstants.BaseAddress), url, out var resolved)
            && (resolved.Scheme == Uri.UriSchemeHttp || resolved.Scheme == Uri.UriSchemeHttps)
            ? resolved.AbsoluteUri
            : null;
    }
}
