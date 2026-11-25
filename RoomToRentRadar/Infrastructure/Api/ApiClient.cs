using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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

    public async Task<TResponse?> PostMultipartAsync<TResponse>(string endpoint, MultipartFormDataContent content, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest &&
            await ReadValidationFieldsAsync(response.Content, cancellationToken) is { Length: > 0 } fields)
        {
            throw new ApiValidationException(fields);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
    }

    private static async Task<string[]?> ReadValidationFieldsAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentType?.MediaType is not ("application/json" or "application/problem+json"))
        {
            return null;
        }

        try
        {
            var problem = await content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (problem.ValueKind == JsonValueKind.Object &&
                problem.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Object)
            {
                return errors.EnumerateObject().Select(field => field.Name).ToArray();
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or
            NotSupportedException or IOException or HttpRequestException)
        {
            // Retain the ordinary HTTP error when the server does not return a readable validation envelope.
        }

        return null;
    }

    public async Task DeleteAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
