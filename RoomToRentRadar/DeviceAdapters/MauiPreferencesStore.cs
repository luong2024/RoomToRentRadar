using Microsoft.Maui.Storage;
using RoomToRentRadar.Client.Adapters.Abstractions;

namespace RoomToRentRadar.DeviceAdapters;

/// <summary><see cref="IPreferencesStore"/> over the platform preferences (erd §5).</summary>
public sealed class MauiPreferencesStore : IPreferencesStore
{
    public string? GetString(string key) => Preferences.Default.Get<string?>(key, null);

    public void SetString(string key, string value) => Preferences.Default.Set(key, value);

    public void Remove(string key) => Preferences.Default.Remove(key);
}
