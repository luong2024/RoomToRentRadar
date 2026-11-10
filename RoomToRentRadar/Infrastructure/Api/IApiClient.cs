namespace RoomToRentRadar.Infrastructure.Api;

/// <summary>
/// Shared HTTP API client contract for communicating with the RoomToRentRadar Backend API.
/// </summary>
public interface IApiClient
{
    Task<TResponse?> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken = default);
}
