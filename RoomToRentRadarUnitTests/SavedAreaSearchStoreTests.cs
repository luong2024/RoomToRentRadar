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

    [Test]
    public void FileStore_Reopening_RetainsVerticesAndSort()
    {
        using var files = new TemporarySnapshotDirectory();
        var saved = CreateArea() with { Sort = RoomToRentRadar.Shared.Listings.ListingSortWire.PriceAsc };
        new FileSavedAreaSearchStore(files.Path).ReplaceAll([saved]);

        var loaded = new FileSavedAreaSearchStore(files.Path).GetAll();

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Has.Count.EqualTo(1));
            Assert.That(loaded[0].Vertices, Is.EqualTo(saved.Vertices));
            Assert.That(loaded[0].Sort, Is.EqualTo(saved.Sort));
            Assert.That(Directory.GetFiles(files.Path, "*.tmp"), Is.Empty);
        });
    }

    [Test]
    public void FileStore_MigratesLegacyPreferences_AndKeepsRecoverySource()
    {
        using var files = new TemporarySnapshotDirectory();
        var preferences = new FakePreferences();
        var saved = CreateArea();
        new PreferencesSavedAreaSearchStore(preferences).ReplaceAll([saved]);
        var legacyJson = preferences.Get(PreferencesSavedAreaSearchStore.PreferencesKey, "");

        var migrated = new FileSavedAreaSearchStore(files.Path, preferences).GetAll();

        Assert.Multiple(() =>
        {
            Assert.That(migrated.Single().Vertices, Is.EqualTo(saved.Vertices));
            Assert.That(migrated.Single().Sort, Is.EqualTo(RoomToRentRadar.Shared.Listings.ListingSortWire.DistanceAsc));
            Assert.That(File.Exists(System.IO.Path.Combine(files.Path, FileSavedAreaSearchStore.FileName)), Is.True);
            Assert.That(preferences.Get(PreferencesSavedAreaSearchStore.PreferencesKey, ""), Is.EqualTo(legacyJson));
        });
        new FileSavedAreaSearchStore(files.Path, preferences).ReplaceAll([]);
        Assert.That(new FileSavedAreaSearchStore(files.Path, preferences).GetAll(), Is.Empty,
            "An empty saved snapshot must not re-import deleted legacy searches.");
    }

    [Test]
    public void FileStore_CorruptSnapshot_CannotBeOverwrittenBySave()
    {
        using var files = new TemporarySnapshotDirectory();
        var destination = System.IO.Path.Combine(files.Path, FileSavedAreaSearchStore.FileName);
        File.WriteAllText(destination, "broken JSON");
        var store = new FileSavedAreaSearchStore(files.Path);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidDataException>(() => store.GetAll());
            Assert.Throws<InvalidDataException>(() => store.ReplaceAll([CreateArea()]));
            Assert.That(File.ReadAllText(destination), Is.EqualTo("broken JSON"));
        });
    }

    [Test]
    public void FileStore_LegacyDuplicateNamesAndIds_AreRenamedWhileInvalidGeometryIsSkipped()
    {
        using var files = new TemporarySnapshotDirectory();
        var preferences = new FakePreferences();
        var longName = new string('a', SavedAreaSearch.MaximumNameLength + 5);
        var legacy = new[]
        {
            CreateArea() with { Name = "  Khu vực  " },
            CreateArea() with { Name = "KHU VỰC" },
            CreateArea() with { Id = "invalid", Vertices = [new(10, 106), new(11, 107), new(12, 108)] },
            CreateArea() with { Id = "long-1", Name = longName },
            CreateArea() with { Id = "long-2", Name = longName }
        };
        new PreferencesSavedAreaSearchStore(preferences).ReplaceAll(legacy);
        var legacyJson = preferences.Get(PreferencesSavedAreaSearchStore.PreferencesKey, "");

        var migrated = new FileSavedAreaSearchStore(files.Path, preferences).GetAll();

        Assert.Multiple(() =>
        {
            Assert.That(migrated, Has.Count.EqualTo(4));
            Assert.That(migrated.Take(2).Select(area => area.Name), Is.EqualTo(new[] { "Khu vực", "KHU VỰC (2)" }));
            Assert.That(migrated.Select(area => area.Id).Distinct().Count(), Is.EqualTo(4));
            Assert.That(migrated.All(area => area.IsValid), Is.True);
            Assert.That(migrated[3].Name, Does.EndWith(" (2)"));
            Assert.That(migrated[3].Name.Length, Is.EqualTo(SavedAreaSearch.MaximumNameLength));
            Assert.That(preferences.Get(PreferencesSavedAreaSearchStore.PreferencesKey, ""), Is.EqualTo(legacyJson));
        });
        var reopened = new FileSavedAreaSearchStore(files.Path, preferences).GetAll();
        Assert.That(reopened.Select(area => area.Id), Is.EqualTo(migrated.Select(area => area.Id)));
    }

    [Test]
    public void FileStore_LegacyOverflow_MigratesBoundedValidSearchesWithoutRemovingSource()
    {
        using var files = new TemporarySnapshotDirectory();
        var preferences = new FakePreferences();
        var legacy = Enumerable.Range(0, SavedAreaSearch.MaximumCount + 3)
            .Select(index => CreateArea() with { Id = index.ToString(), Name = $"Area {index}" }).ToArray();
        new PreferencesSavedAreaSearchStore(preferences).ReplaceAll(legacy);
        var legacyJson = preferences.Get(PreferencesSavedAreaSearchStore.PreferencesKey, "");

        var migrated = new FileSavedAreaSearchStore(files.Path, preferences).GetAll();

        Assert.Multiple(() =>
        {
            Assert.That(migrated, Has.Count.EqualTo(SavedAreaSearch.MaximumCount));
            Assert.That(migrated.Select(area => area.Id), Is.EqualTo(legacy.Take(SavedAreaSearch.MaximumCount).Select(area => area.Id)));
            Assert.That(preferences.Get(PreferencesSavedAreaSearchStore.PreferencesKey, ""), Is.EqualTo(legacyJson));
        });
    }

    [Test]
    public void FileStore_MigrationWriteFailure_PreservesLegacySnapshot()
    {
        using var files = new TemporarySnapshotDirectory();
        var preferences = new FakePreferences();
        new PreferencesSavedAreaSearchStore(preferences).ReplaceAll([CreateArea()]);
        var legacyJson = preferences.Get(PreferencesSavedAreaSearchStore.PreferencesKey, "");
        var blockedDirectory = System.IO.Path.Combine(files.Path, "not-a-directory");
        File.WriteAllText(blockedDirectory, "existing file");

        Assert.Throws<IOException>(() => new FileSavedAreaSearchStore(blockedDirectory, preferences).GetAll());
        Assert.That(preferences.Get(PreferencesSavedAreaSearchStore.PreferencesKey, ""), Is.EqualTo(legacyJson));
    }

    [Test]
    public void FileStore_CorruptLegacySnapshot_IsNotTreatedAsEmpty()
    {
        using var files = new TemporarySnapshotDirectory();
        var preferences = new FakePreferences();
        preferences.Set(PreferencesSavedAreaSearchStore.PreferencesKey, "{broken");
        var store = new FileSavedAreaSearchStore(files.Path, preferences);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidDataException>(() => store.GetAll());
            Assert.Throws<InvalidDataException>(() => store.ReplaceAll([CreateArea()]));
            Assert.That(preferences.Get(PreferencesSavedAreaSearchStore.PreferencesKey, ""), Is.EqualTo("{broken"));
            Assert.That(Directory.GetFiles(files.Path), Is.Empty);
        });
    }

    [Test]
    public void FileStore_DuplicateOrOversizedSearches_DoNotReplaceExistingSnapshot()
    {
        using var files = new TemporarySnapshotDirectory();
        var store = new FileSavedAreaSearchStore(files.Path);
        var saved = CreateArea();
        store.ReplaceAll([saved]);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidDataException>(() => store.ReplaceAll([saved, saved with { Id = "different", Name = "  KHU VỰC  " }]));
            Assert.Throws<InvalidDataException>(() => store.ReplaceAll([saved with { Name = new string('a', SavedAreaSearch.MaximumNameLength + 1) }]));
            Assert.Throws<InvalidDataException>(() => store.ReplaceAll(Enumerable.Range(0, SavedAreaSearch.MaximumCount + 1)
                .Select(index => saved with { Id = index.ToString(), Name = index.ToString() }).ToArray()));
            Assert.That(store.GetAll().Single().Name, Is.EqualTo(saved.Name));
        });
    }

    [Test]
    public void FileStore_MultipleLargePolygons_ExceedPreferenceValueSizeButRoundTrip()
    {
        using var files = new TemporarySnapshotDirectory();
        var polygon = Enumerable.Range(0, SavedAreaSearch.MaximumVertices)
            .Select(index => new GeoPoint(10 + 0.01 * Math.Sin(index * Math.PI / 100),
                106 + 0.01 * Math.Cos(index * Math.PI / 100))).ToArray();
        var areas = Enumerable.Range(0, 4)
            .Select(index => CreateArea() with { Id = index.ToString(), Name = $"Area {index}", Vertices = polygon }).ToArray();
        new FileSavedAreaSearchStore(files.Path).ReplaceAll(areas);

        Assert.Multiple(() =>
        {
            Assert.That(new FileInfo(System.IO.Path.Combine(files.Path, FileSavedAreaSearchStore.FileName)).Length, Is.GreaterThan(8192));
            Assert.That(new FileSavedAreaSearchStore(files.Path).GetAll(), Has.Count.EqualTo(4));
        });
    }

    [Test]
    public void SavedAreaSearch_InvalidGeometry_IsRejected()
    {
        Assert.Multiple(() =>
        {
            Assert.That((CreateArea() with { Vertices = [new(10, 106), new(11, 107), new(12, 108)] }).IsValid, Is.False);
            Assert.That((CreateArea() with { Vertices = [new(10, 106), new(11, 107), new(11, 106), new(10, 107)] }).IsValid, Is.False);
        });
    }

    private static SavedAreaSearch CreateArea()
        => new("area-1", "Khu vực", Vertices, DateTime.UtcNow);

    private sealed class TemporarySnapshotDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "RoomRadarSavedAreaSearchTests", Guid.NewGuid().ToString("N"));

        public TemporarySnapshotDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
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
