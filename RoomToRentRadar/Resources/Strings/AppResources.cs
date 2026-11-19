using System.Globalization;
using System.Resources;

namespace RoomToRentRadar.Resources.Strings;

public static class AppResources
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager("RoomToRentRadar.Resources.Strings.AppResources", typeof(AppResources).Assembly);

    public static CultureInfo? Culture { get; set; }

    public static string GetString(string name)
    {
        return ResourceManager.GetString(name, Culture) ?? $"[{name}]";
    }

    public static string GetString(string name, CultureInfo? culture)
    {
        return ResourceManager.GetString(name, culture ?? Culture) ?? $"[{name}]";
    }
}
