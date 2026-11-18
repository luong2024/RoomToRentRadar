using System.Globalization;
using System.Resources;

namespace RoomToRentRadar.Resources.Strings;

/// <summary>
/// Strongly-typed resource class for accessing localized strings across all platforms.
/// </summary>
public static class AppResources
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "RoomToRentRadar.Resources.Strings.AppResources",
            typeof(AppResources).Assembly);

    public static string GetString(string key, CultureInfo? culture = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return ResourceManager.GetString(key, culture ?? CultureInfo.CurrentUICulture) ?? key;
    }
}
