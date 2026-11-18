namespace RoomToRentRadar.Infrastructure.Api;

/// <summary>
/// Shared HTTP API client contract for communicating with the RoomToRentRadar Backend API.
/// </summary>
public interface IApiClient
{
    Task<TResponse?> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken = default);
    Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest request, CancellationToken cancellationToken = default);
    Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest request, CancellationToken cancellationToken = default) => Task.FromResult<TResponse?>(default);
}
