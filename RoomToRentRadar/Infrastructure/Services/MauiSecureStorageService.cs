using RoomToRentRadar.Core.Interfaces;

namespace RoomToRentRadar.Infrastructure.Services;

/// <summary>
/// Implements ISecureStorageService wrapping Microsoft.Maui.Storage.ISecureStorage.
/// </summary>
public class MauiSecureStorageService : ISecureStorageService
{
    private readonly ISecureStorage _secureStorage;

    public MauiSecureStorageService(ISecureStorage secureStorage)
    {
        _secureStorage = secureStorage;
    }

    public Task<string?> GetAsync(string key) => _secureStorage.GetAsync(key);

    public Task SetAsync(string key, string value) => _secureStorage.SetAsync(key, value);

    public bool Remove(string key) => _secureStorage.Remove(key);

    public void RemoveAll() => _secureStorage.RemoveAll();
}
