namespace RoomToRentRadar.Infrastructure.Api;

/// <summary>
/// Provides environment-specific API base URLs for local development and release builds.
/// </summary>
public static class ApiConfiguration
{
    private const string ApiBaseUrlPreferenceKey = "ApiBaseUrl";

#if DEBUG
    // Uses USB adb port-forwarding (`adb reverse tcp:5000 tcp:5000`); override via Preferences["ApiBaseUrl"] for Wi-Fi LAN testing.
    private const string DefaultPhysicalAndroidDevUrl = "http://127.0.0.1:5000/";
    private const string DefaultEmulatorAndroidDevUrl = "http://10.0.2.2:5000/";
    private const string DefaultLocalDevUrl = "http://localhost:5000/";
#else
    private const string ProductionApiUrl = "https://api.roomtorentradar.com/";
#endif

    public static Uri GetBaseAddress()
    {
#if DEBUG
        var configuredUrl = Preferences.Default.Get<string?>(ApiBaseUrlPreferenceKey, null);
        if (!string.IsNullOrWhiteSpace(configuredUrl) &&
            Uri.TryCreate(configuredUrl, UriKind.Absolute, out var customUri))
        {
            return customUri;
        }

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
}
