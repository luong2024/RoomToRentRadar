using System.Net.Http.Json;

namespace RoomToRentRadar.Infrastructure.Api;

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<TResponse?> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<TResponse>(endpoint, cancellationToken);
    }

    public async Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(endpoint, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
    }
}
