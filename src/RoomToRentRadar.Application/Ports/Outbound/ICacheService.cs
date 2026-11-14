namespace RoomToRentRadar.Application.Ports.Outbound;

/// <summary>
/// Outbound port for cache-aside read-heavy query optimization (AC3).
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
