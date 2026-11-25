namespace RoomToRentRadar.Services.Api
{
    /// <summary>
    /// Where the MAUI app (frontend) finds the backend Web API running in Docker (docker-compose.yml, port 8080).
    /// The default address depends on the platform because "localhost" means different machines:
    ///   - Windows app:       http://localhost:8080   (same PC as Docker)
    ///   - Android emulator:  http://10.0.2.2:8080    (10.0.2.2 is the emulator's alias for the host PC)
    ///   - Real phone:        http://&lt;PC LAN IP&gt;:8080 — set it once with <see cref="SetOverride"/> (e.g. from a settings screen).
    /// </summary>
    public static class ApiEndpoint
    {
        public const int DefaultPort = 8080;

        // Preferences key for a user-chosen API address (real devices on the LAN).
        private const string OverrideKey = "api_base_url";

        /// <summary>Base address with a trailing slash, ready for HttpClient.BaseAddress.</summary>
        public static Uri BaseAddress
        {
            get
            {
                var custom = Preferences.Default.Get(OverrideKey, string.Empty);
                if (TryNormalize(custom, out var overrideUri))
                {
                    return overrideUri;
                }

                var host = DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.DeviceType == DeviceType.Virtual
                    ? "10.0.2.2"   // Android emulator → host PC
                    : "localhost"; // Windows (and other desktop targets) → this PC
                return new Uri($"http://{host}:{DefaultPort}/");
            }
        }

        /// <summary>
        /// Saves a custom API address such as "http://192.168.1.10:8080". Returns false if it is not a valid http/https URL.
        /// Pass null or empty to go back to the platform default.
        /// </summary>
        public static bool SetOverride(string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                Preferences.Default.Remove(OverrideKey);
                return true;
            }

            if (!TryNormalize(baseUrl, out var uri))
            {
                return false;
            }

            Preferences.Default.Set(OverrideKey, uri.ToString());
            return true;
        }

        private static bool TryNormalize(string? value, out Uri uri)
        {
            uri = null!;
            if (string.IsNullOrWhiteSpace(value)
                || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsed)
                || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                return false;
            }

            // HttpClient joins relative paths correctly only when the base ends with "/".
            var text = parsed.GetLeftPart(UriPartial.Path);
            uri = new Uri(text.EndsWith('/') ? text : text + "/");
            return true;
        }
    }
}
