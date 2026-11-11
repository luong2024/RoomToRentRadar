namespace RoomToRentRadar.Infrastructure.Api;

/// <summary>
/// Central configuration for the RoomToRentRadar Backend API base address and
/// the factory that builds the shared <see cref="HttpClient"/>.
/// </summary>
public static class ApiSettings
{
    /// <summary>
    /// Base address used by Release builds.
    /// Replace with the deployed Backend API URL before publishing.
    /// </summary>
    public const string ReleaseBaseUrl = "http://localhost:5000/";

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

    /// <summary>
    /// Gets the base address for the current build configuration and platform.
    /// </summary>
    public static string BaseUrl =>
#if DEBUG && ANDROID
        AndroidEmulatorDebugBaseUrl;
#elif DEBUG
        DebugBaseUrl;
#else
        ReleaseBaseUrl;
#endif

    /// <summary>
    /// Creates an <see cref="HttpClient"/> with an absolute <see cref="HttpClient.BaseAddress"/>
    /// so relative endpoints such as <c>api/rooms</c> resolve correctly.
    /// </summary>
    public static HttpClient CreateHttpClient(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var normalized = baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var baseAddress))
        {
            throw new ArgumentException($"API base URL must be absolute: '{baseUrl}'.", nameof(baseUrl));
        }

        return new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = DefaultTimeout
        };
    }
}
