namespace RoomToRentRadar.Client.Adapters.Abstractions;

/// <summary>Non-secret device preferences (erd §5). Secrets belong in <see cref="ISecureStorageProvider"/>.</summary>
public interface IPreferencesStore
{
    string? GetString(string key);

    void SetString(string key, string value);

    void Remove(string key);
}
