namespace RoomToRentRadar.Core.Interfaces;

/// <summary>
/// Abstraction for platform secure key-value storage.
/// </summary>
public interface ISecureStorageService
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    bool Remove(string key);

    void RemoveAll();
}
