using System.Collections.Concurrent;
using RoomToRentRadar.Client.Adapters.Abstractions;

namespace RoomToRentRadar.Client.Adapters.Mocks;

/// <summary>Volatile <see cref="IPreferencesStore"/> for tests and previews.</summary>
public sealed class InMemoryPreferencesStore : IPreferencesStore
{
    private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);

    public string? GetString(string key) => _values.TryGetValue(key, out var value) ? value : null;

    public void SetString(string key, string value) => _values[key] = value;

    public void Remove(string key) => _values.TryRemove(key, out _);
}
