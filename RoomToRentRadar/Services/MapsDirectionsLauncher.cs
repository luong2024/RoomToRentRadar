using System.Globalization;
using Microsoft.Maui.ApplicationModel;

namespace RoomToRentRadar.Services;

public interface IMapsDirectionsLauncher
{
    Task<bool> OpenAsync(double latitude, double longitude, string? label);
}

public sealed class MapsDirectionsLauncher(ILauncher launcher) : IMapsDirectionsLauncher
{
    public async Task<bool> OpenAsync(double latitude, double longitude, string? label)
    {
        var coordinates = string.Create(
            CultureInfo.InvariantCulture,
            $"{latitude:R},{longitude:R}");

        try
        {
            var geoUri = string.IsNullOrWhiteSpace(label)
                ? new Uri($"geo:{coordinates}")
                : new Uri($"geo:{coordinates}?q={coordinates}({Uri.EscapeDataString(label)})");
            if (await launcher.TryOpenAsync(geoUri))
            {
                return true;
            }
        }
        catch (Exception)
        {
            // Some platforms reject the geo scheme; fall through to the web link.
        }

        try
        {
            return await launcher.TryOpenAsync(new Uri(
                $"https://www.google.com/maps/dir/?api=1&destination={coordinates}"));
        }
        catch (Exception)
        {
            return false;
        }
    }
}
