using Microsoft.Maui.Storage;

namespace RoomToRentRadar.DeviceAdapters;

public static class EnvLoader
{
    public static void Load()
    {
        try
        {
            // 1. Try file system on host (Windows / Desktop debug)
            if (File.Exists(".env"))
            {
                ParseLines(File.ReadAllLines(".env"));
                return;
            }

            if (File.Exists("../.env"))
            {
                ParseLines(File.ReadAllLines("../.env"));
                return;
            }

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, ".env");
                if (File.Exists(candidate))
                {
                    ParseLines(File.ReadAllLines(candidate));
                    return;
                }
            }

            // 2. Try bundled app package asset (Android / iOS)
            // Only load client-specific configuration asset (client.env)
            string? assetName = null;
            if (FileSystem.AppPackageFileExistsAsync("client.env").GetAwaiter().GetResult())
            {
                assetName = "client.env";
            }

            if (assetName != null)
            {
                using var stream = FileSystem.OpenAppPackageFileAsync(assetName).GetAwaiter().GetResult();
                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();
                ParseLines(content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EnvLoader] Error loading env: {ex.Message}");
        }
    }

    private static void ParseLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim().Trim('"', '\'');

            if (!string.IsNullOrEmpty(key))
            {
                Environment.SetEnvironmentVariable(key, value);
                Preferences.Default.Set(key, value);

                // Support normalized keys
                if (key.Equals("GOOGLE_CLIENT_ID", StringComparison.OrdinalIgnoreCase))
                {
                    Preferences.Default.Set("GoogleClientId", value);
                }
                else if (key.Equals("GOOGLE_REDIRECT_URI", StringComparison.OrdinalIgnoreCase))
                {
                    Preferences.Default.Set("GoogleRedirectUri", value);
                }
            }
        }
    }
}
