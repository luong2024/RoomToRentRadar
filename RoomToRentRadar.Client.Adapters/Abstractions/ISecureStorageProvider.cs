namespace RoomToRentRadar.Client.Adapters.Abstractions;

public interface ISecureStorageProvider
{
    Task SetAsync(string key, string value);
    Task<string?> GetAsync(string key);
    bool Remove(string key);
    void RemoveAll();
}

