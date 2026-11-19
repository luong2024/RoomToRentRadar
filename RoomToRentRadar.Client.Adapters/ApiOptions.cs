namespace RoomToRentRadar.Client.Adapters;

/// <summary>Connection settings shared by every HTTP gateway (ADR-003).</summary>
public sealed class ApiOptions
{
    public const string AndroidEmulatorBaseUrl = "http://10.0.2.2:5000";
    public const string LocalhostBaseUrl = "http://localhost:5000";

    /// <summary>Must be a root URL: gateways use absolute "/api/..." paths, which silently drop any base path.</summary>
    public Uri BaseUrl { get; set; } = new(AndroidEmulatorBaseUrl);

    /// <summary>Data-only flag; the "Debug only" decision lives in the MAUI factory, not here (tests run Release).</summary>
    public bool UseMocks { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    public void Validate(bool requireHttps = false)
    {
        if (BaseUrl is null || !IsAllowedBaseUrl(BaseUrl))
        {
            throw new ArgumentException(
                $"BaseUrl must be an absolute http/https root URL without path, query or fragment: '{BaseUrl}'.",
                nameof(BaseUrl));
        }

        if (requireHttps && BaseUrl.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"BaseUrl must use https: '{BaseUrl}'.");
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException($"Timeout must be positive: '{Timeout}'.", nameof(Timeout));
        }
    }

    public static bool TryNormalizeBaseUrl(string? raw, bool requireHttps, out Uri? url)
    {
        url = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim().TrimEnd('/');
        if (trimmed.Length == 0
            || !Uri.TryCreate(trimmed, UriKind.Absolute, out var candidate)
            || !IsAllowedBaseUrl(candidate))
        {
            return false;
        }

        if (requireHttps && candidate.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        url = candidate;
        return true;
    }

    private static bool IsAllowedBaseUrl(Uri url) =>
        url.IsAbsoluteUri
        && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
        && url.AbsolutePath == "/"
        && string.IsNullOrEmpty(url.Query)
        && string.IsNullOrEmpty(url.Fragment);
}
