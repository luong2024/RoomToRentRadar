using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Client.Adapters.Http;

public sealed class HttpAuthGateway : IAuthGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpAuthGateway>? _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public HttpAuthGateway(HttpClient httpClient, ILogger<HttpAuthGateway>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    public async Task<AuthTokensDto> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request, JsonOptions, ct);
        return await HandleResponseAsync<AuthTokensDto>(response, ct);
    }

    public async Task<AuthTokensDto> RegisterAsync(RegisterUserRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request, JsonOptions, ct);
        return await HandleResponseAsync<AuthTokensDto>(response, ct);
    }

    public async Task<AuthTokensDto> LoginWithIctuSsoAsync(IctuSsoLoginRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = await _httpClient.PostAsJsonAsync("/api/auth/sso/ictu", request, JsonOptions, ct);
        return await HandleResponseAsync<AuthTokensDto>(response, ct);
    }

    public async Task<AuthTokensDto> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = await _httpClient.PostAsJsonAsync("/api/auth/refresh", request, JsonOptions, ct);
        return await HandleResponseAsync<AuthTokensDto>(response, ct);
    }

    public async Task RevokeTokenAsync(RevokeTokenRequest request, string? bearerToken = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/auth/revoke")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        var response = await _httpClient.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, ct);
        }
    }

    public async Task<UserProfileDto> GetCurrentUserProfileAsync(string? bearerToken = null, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        var response = await _httpClient.SendAsync(message, ct);
        return await HandleResponseAsync<UserProfileDto>(response, ct);
    }

    private async Task<T> HandleResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, ct);
        }

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        if (result == null)
        {
            throw new ApiException("Server returned an empty body.", response.StatusCode);
        }

        return result;
    }

    private async Task ThrowApiExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string? rawBody = null;
        string? errorCode = null;
        string? detail = null;
        Dictionary<string, string[]>? errors = null;

        try
        {
            rawBody = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(rawBody))
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;

                if (root.TryGetProperty("errorCode", out var codeElem))
                {
                    errorCode = codeElem.GetString();
                }
                else if (root.TryGetProperty("extensions", out var extElem) && extElem.ValueKind == JsonValueKind.Object)
                {
                    if (extElem.TryGetProperty("errorCode", out var extCodeElem))
                    {
                        errorCode = extCodeElem.GetString();
                    }
                }

                if (root.TryGetProperty("detail", out var detailElem))
                {
                    detail = detailElem.GetString();
                }
                else if (root.TryGetProperty("title", out var titleElem))
                {
                    detail = titleElem.GetString();
                }

                if (root.TryGetProperty("errors", out var errorsElem) && errorsElem.ValueKind == JsonValueKind.Object)
                {
                    errors = new Dictionary<string, string[]>();
                    foreach (var prop in errorsElem.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            var list = new List<string>();
                            foreach (var item in prop.Value.EnumerateArray())
                            {
                                var str = item.GetString();
                                if (str != null) list.Add(str);
                            }
                            errors[prop.Name] = list.ToArray();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse API error response body as JSON ProblemDetails");
        }

        var message = detail ?? errorCode ?? $"HTTP error {(int)response.StatusCode} ({response.ReasonPhrase})";
        _logger?.LogWarning("API call failed with status {StatusCode}, errorCode {ErrorCode}: {Message}",
            response.StatusCode, errorCode, message);

        throw new ApiException(message, response.StatusCode, errorCode, errors);
    }
}

