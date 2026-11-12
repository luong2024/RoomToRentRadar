using System.Text.Json;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services;

public interface ISavedAreaSearchStore
{
    IReadOnlyList<SavedAreaSearch> GetAll();

    void ReplaceAll(IReadOnlyList<SavedAreaSearch> areas);
}

public sealed class PreferencesSavedAreaSearchStore(IPreferences preferences) : ISavedAreaSearchStore
{
    public const string PreferencesKey = "map.saved_area_searches.v1";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<SavedAreaSearch> GetAll()
    {
        var json = preferences.Get(PreferencesKey, "");
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var areas = JsonSerializer.Deserialize<List<SavedAreaSearch>>(json, SerializerOptions);
            return areas is null ? [] : areas.Where(IsUsable).ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public void ReplaceAll(IReadOnlyList<SavedAreaSearch> areas)
        => preferences.Set(PreferencesKey, JsonSerializer.Serialize(areas, SerializerOptions));

    private static bool IsUsable(SavedAreaSearch? area)
        => area is { Id.Length: > 0, Name.Length: > 0 }
            && area.Vertices is { Count: >= PolygonDraft.MinimumVertices }
            && area.Vertices.All(vertex => vertex.IsValid);
}

/// <summary>
/// Stores bounded polygon snapshots outside Preferences' per-value size limits.
/// Existing preference data is copied once; its source is retained for recovery.
/// </summary>
public sealed class FileSavedAreaSearchStore : ISavedAreaSearchStore
{
    public const string FileName = "map-saved-area-searches.v1.json";
    private const int MaximumSnapshotBytes = 1_048_576;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly object _gate = new();
    private readonly string _directoryPath;
    private readonly string _filePath;
    private readonly IPreferences? _legacyPreferences;

    public FileSavedAreaSearchStore(string directoryPath, IPreferences? legacyPreferences = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        _directoryPath = Path.GetFullPath(directoryPath);
        _filePath = Path.Combine(_directoryPath, FileName);
        _legacyPreferences = legacyPreferences;
    }

    public IReadOnlyList<SavedAreaSearch> GetAll()
    {
        lock (_gate)
            return ReadOrMigrate();
    }

    public void ReplaceAll(IReadOnlyList<SavedAreaSearch> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        Validate(areas);
        // Copy so callers cannot mutate the list while a snapshot is written.
        var snapshot = areas.Select(area => area with { Vertices = area.Vertices.ToArray() }).ToArray();
        Validate(snapshot);
        lock (_gate)
        {
            // A failed read must never be mistaken for an empty store and then
            // overwritten by a subsequent Save or Delete.
            ReadOrMigrate();
            WriteSnapshot(snapshot);
        }
    }

    private IReadOnlyList<SavedAreaSearch> ReadOrMigrate()
    {
        if (File.Exists(_filePath))
        {
            if (new FileInfo(_filePath).Length > MaximumSnapshotBytes)
                throw new InvalidDataException("Saved area snapshot exceeds its size limit.");
            try
            {
                var snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllBytes(_filePath), SerializerOptions);
                if (snapshot is not { Version: 1, Areas: not null })
                    throw new InvalidDataException("Unsupported saved area snapshot.");
                Validate(snapshot.Areas);
                return snapshot.Areas;
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Saved area snapshot is corrupt.", ex);
            }
        }

        var legacyJson = _legacyPreferences?.Get(PreferencesSavedAreaSearchStore.PreferencesKey, "");
        if (string.IsNullOrWhiteSpace(legacyJson))
            return [];
        if (System.Text.Encoding.UTF8.GetByteCount(legacyJson) > MaximumSnapshotBytes)
            throw new InvalidDataException("Legacy saved area snapshot exceeds its size limit.");
        SavedAreaSearch[] migrated;
        try
        {
            migrated = JsonSerializer.Deserialize<SavedAreaSearch[]>(legacyJson, SerializerOptions)
                ?? throw new InvalidDataException("Legacy saved area snapshot is invalid.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Legacy saved area snapshot is corrupt.", ex);
        }
        var usable = NormalizeLegacyAreas(migrated);
        Validate(usable);
        WriteSnapshot(usable);
        return usable;
    }

    private static IReadOnlyList<SavedAreaSearch> NormalizeLegacyAreas(IReadOnlyList<SavedAreaSearch> areas)
    {
        var migrated = new List<SavedAreaSearch>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var area in areas)
        {
            if (area is null || string.IsNullOrWhiteSpace(area.Id) || string.IsNullOrWhiteSpace(area.Name))
                continue;

            var originalName = area.Name.Trim();
            originalName = originalName[..Math.Min(originalName.Length, SavedAreaSearch.MaximumNameLength)];
            var candidate = area with { Name = originalName };
            if (!candidate.IsValid)
                continue;

            var name = originalName;
            for (var ordinal = 2; names.Contains(name); ordinal++)
            {
                var suffix = $" ({ordinal})";
                name = originalName[..Math.Min(originalName.Length, SavedAreaSearch.MaximumNameLength - suffix.Length)] + suffix;
            }
            var id = area.Id;
            for (var ordinal = 2; ids.Contains(id); ordinal++)
                id = $"{area.Id}-migrated-{ordinal}";

            names.Add(name);
            ids.Add(id);
            migrated.Add(candidate with { Name = name, Id = id });
            if (migrated.Count == SavedAreaSearch.MaximumCount)
                break;
        }
        // Legacy Preferences stay untouched so invalid/overflow records remain
        // available for recovery rather than blocking every usable saved search.
        return migrated;
    }

    private static void Validate(IReadOnlyList<SavedAreaSearch> areas)
    {
        if (areas.Count > SavedAreaSearch.MaximumCount
            || areas.Any(area => area is null || !area.IsValid)
            || areas.Select(area => area.Id).Distinct(StringComparer.Ordinal).Count() != areas.Count
            || areas.Select(area => area.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != areas.Count)
            throw new InvalidDataException("Saved area snapshot contains invalid or duplicate searches.");
    }

    private void WriteSnapshot(IReadOnlyList<SavedAreaSearch> areas)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Snapshot(1, areas), SerializerOptions);
        if (bytes.Length > MaximumSnapshotBytes)
            throw new InvalidDataException("Saved area snapshot exceeds its size limit.");
        Directory.CreateDirectory(_directoryPath);
        var temporaryPath = Path.Combine(_directoryPath, $"{FileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            // The flushed temporary file and destination are on the same volume.
            // Readers see either the previous complete snapshot or the new one.
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private sealed record Snapshot(int Version, IReadOnlyList<SavedAreaSearch> Areas);
}
