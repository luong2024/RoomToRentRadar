using Microsoft.Maui.Storage;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class SavedAreaSearchStoreTests
{
    private static readonly GeoPoint[] Vertices =
    [
        new(10.0, 106.0),
        new(10.0, 107.0),
        new(11.0, 107.0)
    ];

    [Test]
    public void ReplaceAll_ThenGetAll_RoundTripsAreas()
    {
        var preferences = new FakePreferences();
        var store = new PreferencesSavedAreaSearchStore(preferences);
        var saved = new SavedAreaSearch(
            "area-1",
            "Quận 10",
            Vertices,
            new DateTime(2026, 11, 12, 8, 30, 0, DateTimeKind.Utc));

        store.ReplaceAll([saved]);
        var loaded = new PreferencesSavedAreaSearchStore(preferences).GetAll();

        Assert.That(loaded, Has.Count.EqualTo(1));
        Assert.That(loaded[0].Id, Is.EqualTo("area-1"));
        Assert.That(loaded[0].Name, Is.EqualTo("Quận 10"));
        Assert.That(loaded[0].Vertices, Is.EqualTo(Vertices));
        Assert.That(loaded[0].SavedAt, Is.EqualTo(saved.SavedAt));
    }

    [Test]
    public void GetAll_WhenNothingIsSaved_ReturnsEmpty()
    {
        var store = new PreferencesSavedAreaSearchStore(new FakePreferences());

        Assert.That(store.GetAll(), Is.Empty);
    }

    [Test]
    public void GetAll_WhenStoredJsonIsCorrupt_ReturnsEmpty()
    {
        var preferences = new FakePreferences();
        preferences.Set(PreferencesSavedAreaSearchStore.PreferencesKey, "{ not json");
        var store = new PreferencesSavedAreaSearchStore(preferences);

        Assert.That(store.GetAll(), Is.Empty);
    }

    [Test]
    public void GetAll_DropsEntriesThatCannotBeUsed()
    {
        var preferences = new FakePreferences();
        preferences.Set(
            PreferencesSavedAreaSearchStore.PreferencesKey,
            """
            [
              { "id": "ok", "name": "Hợp lệ", "vertices": [ { "lat": 10.0, "lng": 106.0 }, { "lat": 10.0, "lng": 107.0 }, { "lat": 11.0, "lng": 107.0 } ], "savedAt": "2026-11-12T08:30:00Z" },
              { "id": "no-vertices", "name": "Thiếu đỉnh", "vertices": null, "savedAt": "2026-11-12T08:30:00Z" },
              { "id": "", "name": "Thiếu Id", "vertices": [ { "lat": 10.0, "lng": 106.0 }, { "lat": 10.0, "lng": 107.0 }, { "lat": 11.0, "lng": 107.0 } ], "savedAt": "2026-11-12T08:30:00Z" },
              { "id": "bad-lat", "name": "Tọa độ sai", "vertices": [ { "lat": 999.0, "lng": 106.0 }, { "lat": 10.0, "lng": 107.0 }, { "lat": 11.0, "lng": 107.0 } ], "savedAt": "2026-11-12T08:30:00Z" }
            ]
            """);
        var store = new PreferencesSavedAreaSearchStore(preferences);

        var loaded = store.GetAll();

        Assert.That(loaded.Select(area => area.Id), Is.EqualTo(new[] { "ok" }));
    }

    private sealed class FakePreferences : IPreferences
    {
        private readonly Dictionary<string, object?> _values = [];

        public bool ContainsKey(string key, string? sharedName = null) => _values.ContainsKey(key);

        public void Remove(string key, string? sharedName = null) => _values.Remove(key);

        public void Clear(string? sharedName = null) => _values.Clear();

        public void Set<T>(string key, T value, string? sharedName = null) => _values[key] = value;

        public T Get<T>(string key, T defaultValue, string? sharedName = null)
            => _values.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;
    }
}
