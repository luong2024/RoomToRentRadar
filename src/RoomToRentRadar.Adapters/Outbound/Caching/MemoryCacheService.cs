namespace RoomToRentRadar.Adapters.Outbound.Caching;

using Microsoft.Extensions.Caching.Memory;
using RoomToRentRadar.Application.Ports.Outbound;

/// <summary>
/// In-process memory cache adapter implementing the outbound port <see cref="ICacheService"/>.
/// <para>
/// <b>Architectural Note for Multi-Instance Deployments:</b><br/>
/// This implementation provides fast in-memory caching for single-instance, local emulator, and development environments.
/// In a multi-instance production environment (e.g. horizontally scaled Kubernetes pods or Azure App Service replicas behind a Load Balancer),
/// instances do not share local process memory. To ensure synchronized cache invalidation across all nodes, register a distributed
/// cache adapter (e.g. Redis / Azure Cache for Redis implementing <see cref="ICacheService"/> via <c>IDistributedCache</c> or Pub/Sub)
/// in the dependency injection container without requiring any modifications to the Domain or Application use cases.
/// </para>
/// </summary>
public sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;

    public MemoryCacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(key, out var val) && val is T typed)
        {
            return Task.FromResult<T?>(typed);
        }

        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var options = new MemoryCacheEntryOptions();
        if (expiration.HasValue)
        {
            options.SetAbsoluteExpiration(expiration.Value);
        }
        else
        {
            options.SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
        }

        _memoryCache.Set(key, value, options);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(key);
        return Task.CompletedTask;
    }
}
