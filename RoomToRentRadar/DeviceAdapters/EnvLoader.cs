namespace RoomToRentRadar.DeviceAdapters;

public static class EnvLoader
{
    private static readonly HashSet<string> AllowedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "GOOGLE_CLIENT_ID",
        "GOOGLE_REDIRECT_URI",
        "API_BASE_URL"
    };

    public static void Load()
    {
        try
        {
            // 1. Try local client.env or .env in current directory only (never traverse parent folders)
            if (File.Exists("client.env"))
            {
                ParseLines(File.ReadAllLines("client.env"));
                return;
            }

            if (File.Exists(".env"))
            {
                ParseLines(File.ReadAllLines(".env"));
                return;
            }

            // 2. Try packaged asset client.env
            try
            {
                using var stream = FileSystem.OpenAppPackageFileAsync("client.env")
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();
                ParseLines(content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            }
            catch (FileNotFoundException)
            {
                // Optional client.env asset
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EnvLoader] Error loading client env: {ex.Message}");
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

            if (!string.IsNullOrEmpty(key) && AllowedKeys.Contains(key))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
