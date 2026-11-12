namespace RoomToRentRadar.Infrastructure.Api;

/// <summary>
/// Central configuration for the RoomToRentRadar Backend API base address and
/// the factory that builds the shared <see cref="HttpClient"/>.
/// </summary>
public static class ApiSettings
{
    /// <summary>
    /// Optional environment variable name that overrides the default API base URL.
    /// </summary>
    public const string BaseUrlEnvironmentVariable = "ROOMTORENTRADAR_API_BASE_URL";

    /// <summary>
    /// Base address used by Release builds (must use HTTPS and target a remote host).
    /// </summary>
    public const string ReleaseBaseUrl = "https://api.roomtorentradar.com/";

    /// <summary>
    /// Base address used by Debug builds on desktop/iOS simulators (Kestrel default port).
    /// </summary>
    public const string DebugBaseUrl = "http://localhost:5000/";

    /// <summary>
    /// Base address used by Debug builds on the Android emulator, where the host
    /// machine's loopback interface is exposed as 10.0.2.2.
    /// </summary>
    public const string AndroidEmulatorDebugBaseUrl = "http://10.0.2.2:5000/";

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private static readonly HashSet<string> LoopbackHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "127.0.0.1",
        "::1",
        "10.0.2.2"
    };

    /// <summary>
    /// Gets the base address for the current build configuration and platform.
    /// </summary>
    public static string BaseUrl
    {
        get
        {
            var overrideUrl = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(overrideUrl))
            {
                return overrideUrl.Trim();
            }

#if DEBUG && ANDROID
            return AndroidEmulatorDebugBaseUrl;
#elif DEBUG
            return DebugBaseUrl;
#else
            return ReleaseBaseUrl;
#endif
        }
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> with an absolute <see cref="HttpClient.BaseAddress"/>
    /// so relative endpoints such as <c>api/rooms</c> resolve correctly.
    /// Release builds enforce HTTPS and reject local loopback hosts.
    /// </summary>
    public static HttpClient CreateHttpClient(string baseUrl, bool? requireHttps = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

#if DEBUG
        bool enforceHttps = requireHttps ?? false;
#else
        bool enforceHttps = requireHttps ?? true;
#endif

        var normalized = baseUrl.Trim();
        if (!normalized.EndsWith('/'))
        {
            normalized += "/";
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var baseAddress) ||
            (baseAddress.Scheme != Uri.UriSchemeHttp && baseAddress.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"API base URL must be an absolute HTTP/HTTPS URI: '{baseUrl}'.", nameof(baseUrl));
        }

        if (enforceHttps)
        {
            if (baseAddress.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException(
                    $"Release API base URL must use HTTPS: '{baseUrl}'.",
                    nameof(baseUrl));
            }

            if (IsLoopbackOrEmulatorHost(baseAddress.Host))
            {
                throw new ArgumentException(
                    $"Release API base URL must not target a local loopback or emulator host: '{baseUrl}'.",
                    nameof(baseUrl));
            }
        }

        return new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = DefaultTimeout
        };
    }

    public static bool IsLoopbackOrEmulatorHost(string? host) =>
        !string.IsNullOrWhiteSpace(host) && LoopbackHosts.Contains(host.Trim());
}
