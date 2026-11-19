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
    private static readonly JsonSerializerOptions JsonOptions = ApiResponseReader.JsonOptions;

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
        return await ApiResponseReader.ReadAsync<T>(response, _logger, ct);
    }

    private async Task ThrowApiExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        throw await ApiResponseReader.CreateExceptionAsync(response, _logger, ct);
    }
}

