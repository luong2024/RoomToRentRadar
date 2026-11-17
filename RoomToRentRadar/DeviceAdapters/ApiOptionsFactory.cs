using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using RoomToRentRadar.Client.Adapters;

namespace RoomToRentRadar.DeviceAdapters;

/// <summary>
/// Builds <see cref="ApiOptions"/> from device state (ADR-003 / ADR-013). Preferences is a MAUI
/// Essentials API, so the override is read here — Client.Adapters must not reference MAUI.
/// Debug: Android emulator uses 10.0.2.2, desktop uses localhost. A Dev Tunnels URL can be stored
/// under the "ApiBaseUrlOverride" Preferences key (or via SetBaseUrlOverride in debug code); it is
/// read once at startup, so restart the app after changing it. On a Debug APK the key can also be
/// written with adb run-as into shared_prefs/&lt;pkg&gt;.microsoft.maui.essentials.preferences.xml.
/// Invalid override values are ignored and the default URL is kept.
/// Release: fixed HTTPS endpoint, no Preferences, fail-fast validation (NFR-05).
/// </summary>
internal static class ApiOptionsFactory
{
    private const string BaseUrlOverrideKey = "ApiBaseUrlOverride";
    private const string UseMocksKey = "UseMocks";
    private const string ReleaseBaseUrl = "https://api.roomtorentradar.com";

    public static ApiOptions Create()
    {
#if DEBUG
        var options = new ApiOptions
        {
            BaseUrl = new Uri(DeviceInfo.Platform == DevicePlatform.Android
                ? ApiOptions.AndroidEmulatorBaseUrl
                : ApiOptions.LocalhostBaseUrl),
            UseMocks = Preferences.Default.Get(UseMocksKey, false)
        };

        var rawOverride = Preferences.Default.Get(BaseUrlOverrideKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(rawOverride))
        {
            if (ApiOptions.TryNormalizeBaseUrl(rawOverride, requireHttps: false, out var overrideUrl))
            {
                options.BaseUrl = overrideUrl!;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ApiOptionsFactory] Ignoring invalid {BaseUrlOverrideKey} '{rawOverride}'; using {options.BaseUrl}.");
            }
        }

        options.Validate();
        return options;
#else
        var options = new ApiOptions
        {
            BaseUrl = new Uri(ReleaseBaseUrl),
            UseMocks = false
        };
        options.Validate(requireHttps: true);
        return options;
#endif
    }

#if DEBUG
    /// <summary>Stores (or clears, when null/empty) the Dev Tunnels override; takes effect on next app start.</summary>
    public static void SetBaseUrlOverride(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Preferences.Default.Remove(BaseUrlOverrideKey);
        }
        else
        {
            Preferences.Default.Set(BaseUrlOverrideKey, url);
        }
    }
#endif
}