namespace RoomToRentRadar.Infrastructure.Api;

/// <summary>
/// Provides environment-specific API base URLs for local development and release builds.
/// </summary>
public static class ApiConfiguration
{
    public const string ApiBaseUrlPreferenceKey = "ApiBaseUrl";
    public const int BackendHttpPort = 49966;
    public const int BackendHttpsPort = 49965;

    public const string ProductionApiUrl = "https://api.roomtorentradar.com/";

#if DEBUG
    // Android physical device uses USB adb port-forwarding: `adb reverse tcp:49966 tcp:49966`.
    // For Wi-Fi LAN testing, set custom URL dynamically via SetCustomBaseUrl() instead of hardcoding IP.
    public const string DefaultPhysicalAndroidDevUrl = "http://127.0.0.1:49966/";
    public const string DefaultEmulatorAndroidDevUrl = "http://10.0.2.2:49966/";
    public const string DefaultLocalDevUrl = "http://localhost:49966/";
#endif

    /// <summary>
    /// Gets the resolved API base URI depending on build configuration, device platform, and user preferences.
    /// </summary>
    public static Uri GetBaseAddress()
    {
        var configuredUrl = Preferences.Default.Get<string?>(ApiBaseUrlPreferenceKey, null);
        if (!string.IsNullOrWhiteSpace(configuredUrl) &&
            Uri.TryCreate(configuredUrl, UriKind.Absolute, out var customUri))
        {
#if DEBUG
            if (customUri.Scheme == Uri.UriSchemeHttp || customUri.Scheme == Uri.UriSchemeHttps)
            {
                return customUri;
            }
#else
            if (customUri.Scheme == Uri.UriSchemeHttps)
            {
                return customUri;
            }
#endif
        }

#if DEBUG
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            var androidUrl = DeviceInfo.DeviceType == DeviceType.Virtual
                ? DefaultEmulatorAndroidDevUrl
                : DefaultPhysicalAndroidDevUrl;
            return new Uri(androidUrl);
        }

        return new Uri(DefaultLocalDevUrl);
#else
        return new Uri(ProductionApiUrl);
#endif
    }

    /// <summary>
    /// Sets a custom API Base URL (e.g. for Wi-Fi LAN testing).
    /// Enforces HTTP/HTTPS in Debug and strictly HTTPS in Release/Production.
    /// </summary>
    public static bool SetCustomBaseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

#if DEBUG
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }
#else
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }
#endif

        var normalizedUrl = uri.ToString();
        if (!normalizedUrl.EndsWith('/'))
        {
            normalizedUrl += "/";
        }

        Preferences.Default.Set(ApiBaseUrlPreferenceKey, normalizedUrl);
        return true;
    }

    /// <summary>
    /// Clears any custom configured base URL, reverting to defaults.
    /// </summary>
    public static void ResetToDefault()
    {
        Preferences.Default.Remove(ApiBaseUrlPreferenceKey);
    }
}
