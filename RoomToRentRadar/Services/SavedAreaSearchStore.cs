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
