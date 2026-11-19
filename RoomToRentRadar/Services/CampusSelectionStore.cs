namespace RoomToRentRadar.Services;

public interface ICampusSelectionStore
{
    string? GetSelectedCampus();

    void SetSelectedCampus(string? campusName);
}

public sealed class PreferencesCampusSelectionStore(IPreferences preferences) : ICampusSelectionStore
{
    public const string PreferencesKey = "travel.selected_campus.v1";

    public string? GetSelectedCampus()
    {
        var value = preferences.Get(PreferencesKey, string.Empty);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public void SetSelectedCampus(string? campusName)
    {
        if (string.IsNullOrWhiteSpace(campusName))
        {
            preferences.Remove(PreferencesKey);
            return;
        }

        preferences.Set(PreferencesKey, campusName.Trim());
    }
}
