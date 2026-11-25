namespace RoomToRentRadar.Client.Adapters.Utilities;

/// <summary>
/// A local API publishes media as <c>http://localhost:5000/media/...</c> (ADR-008 <c>PublicBaseUrl</c>),
/// which an Android emulator or phone cannot reach under that name. Such URLs are moved onto the address
/// the app already uses for the API; every other URL is left untouched.
/// </summary>
public static class MediaUrlRewriter
{
    private const string MediaPathPrefix = "/media/";

    public static string? Rewrite(string? url, Uri? apiBaseAddress)
    {
        if (apiBaseAddress is null
            || !Uri.TryCreate(url, UriKind.Absolute, out var media)
            // The scheme check also rejects "/media/..." being read as a file path on Unix hosts.
            || (media.Scheme != Uri.UriSchemeHttp && media.Scheme != Uri.UriSchemeHttps)
            || !media.IsLoopback
            || !media.AbsolutePath.StartsWith(MediaPathPrefix, StringComparison.Ordinal))
        {
            return url;
        }

        return new UriBuilder(media)
        {
            Scheme = apiBaseAddress.Scheme,
            Host = apiBaseAddress.Host,
            Port = apiBaseAddress.Port
        }.Uri.AbsoluteUri;
    }
}
