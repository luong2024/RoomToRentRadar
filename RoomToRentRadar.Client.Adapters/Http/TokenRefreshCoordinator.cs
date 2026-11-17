using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Client.Adapters.Http;

public static class ApiClientNames
{
    /// <summary>Dedicated refresh client: never has AuthTokenHandler attached, so refresh cannot recurse.</summary>
    public const string Refresh = "RoomToRentRadar.Refresh";
}

public enum RefreshOutcomeKind
{
    NoSession,
    SignedOut,
    Refreshed,
    Failed
}

public sealed record RefreshOutcome(RefreshOutcomeKind Kind, string? AccessToken = null, ApiException? Error = null)
{
    public static RefreshOutcome NoSession() => new(RefreshOutcomeKind.NoSession);

    public static RefreshOutcome SignedOut() => new(RefreshOutcomeKind.SignedOut);

    public static RefreshOutcome Refreshed(string accessToken) => new(RefreshOutcomeKind.Refreshed, accessToken);

    public static RefreshOutcome Failed(ApiException error) => new(RefreshOutcomeKind.Failed, Error: error);
}

/// <summary>
/// Single-flight token refresh shared by every AuthTokenHandler instance (each gateway gets its own
/// handler pipeline, so the lock must live here, not in a handler). The server revokes the whole
/// token chain on refresh-token reuse, so two parallel refresh calls would sign the user out.
/// </summary>
public sealed class TokenRefreshCoordinator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISessionStore _sessionStore;
    private readonly ISessionEvents _sessionEvents;
    private readonly ApiOptions _options;
    private readonly ILogger<TokenRefreshCoordinator>? _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TokenRefreshCoordinator(
        IHttpClientFactory httpClientFactory,
        ISessionStore sessionStore,
        ISessionEvents sessionEvents,
        ApiOptions options,
        ILogger<TokenRefreshCoordinator>? logger = null)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _sessionEvents = sessionEvents ?? throw new ArgumentNullException(nameof(sessionEvents));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <param name="staleAccessToken">The token the 401 request was sent with (null on cold start).</param>
    /// <param name="waitToken">Cancels only the wait for the lock; the refresh itself runs uncancelled.</param>
    public async Task<RefreshOutcome> RefreshAsync(string? staleAccessToken, CancellationToken waitToken)
    {
        await _gate.WaitAsync(waitToken);

        RefreshOutcome outcome;
        bool raiseSessionExpired;
        try
        {
            (outcome, raiseSessionExpired) = await RefreshOnceAsync(staleAccessToken);
        }
        finally
        {
            _gate.Release();
        }

        if (raiseSessionExpired)
        {
            _sessionEvents.RaiseSessionExpired();
        }

        return outcome;
    }

    private async Task<(RefreshOutcome Outcome, bool RaiseSessionExpired)> RefreshOnceAsync(string? staleAccessToken)
    {
        var currentAccessToken = await _sessionStore.GetAccessTokenAsync(CancellationToken.None);
        if (!string.Equals(currentAccessToken, staleAccessToken, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(currentAccessToken))
        {
            // Another request already refreshed while we waited for the lock.
            return (RefreshOutcome.Refreshed(currentAccessToken), false);
        }

        var refreshToken = await _sessionStore.GetRefreshTokenAsync(CancellationToken.None);
        if (string.IsNullOrEmpty(refreshToken))
        {
            return (RefreshOutcome.NoSession(), false);
        }

        HttpResponseMessage response;
        try
        {
            var client = _httpClientFactory.CreateClient(ApiClientNames.Refresh);
            using var timeoutCts = new CancellationTokenSource(_options.Timeout);
            response = await client.PostAsJsonAsync(
                "/api/auth/refresh",
                new RefreshTokenRequest(refreshToken),
                ApiResponseReader.JsonOptions,
                timeoutCts.Token);
        }
        catch (ApiException ex)
        {
            // Already mapped (e.g. by NetworkErrorHandler on the refresh client): temporary, keep the session.
            return (RefreshOutcome.Failed(ex), false);
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or OperationCanceledException)
        {
            _logger?.LogWarning("Token refresh failed without an HTTP response ({ExceptionType}).", ex.GetType().Name);
            return (RefreshOutcome.Failed(ApiResponseReader.FromNetworkException(ex)), false);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                AuthTokensDto tokens;
                try
                {
                    tokens = await ApiResponseReader.ReadAsync<AuthTokensDto>(response, _logger, CancellationToken.None);
                }
                catch (ApiException ex)
                {
                    return (RefreshOutcome.Failed(ex), false);
                }
                catch (JsonException ex)
                {
                    return (RefreshOutcome.Failed(new ApiException(
                        "Server returned an invalid token refresh response.",
                        response.StatusCode,
                        innerException: ex)), false);
                }

                // The user may have signed out while the refresh was in flight: never resurrect a cleared session.
                var currentRefreshToken = await _sessionStore.GetRefreshTokenAsync(CancellationToken.None);
                if (!string.Equals(currentRefreshToken, refreshToken, StringComparison.Ordinal))
                {
                    _logger?.LogInformation("Discarding token refresh result because the session changed while the refresh was in flight.");
                    return (RefreshOutcome.SignedOut(), false);
                }

                var saved = await _sessionStore.SaveSessionAsync(tokens, CancellationToken.None);
                if (!saved)
                {
                    _logger?.LogWarning("Could not persist the refreshed session; continuing with the new access token in memory only.");
                }

                return (RefreshOutcome.Refreshed(tokens.AccessToken), false);
            }

            var error = await ApiResponseReader.CreateExceptionAsync(response, _logger, CancellationToken.None);
            if (IsDefinitiveRejection(response.StatusCode))
            {
                await _sessionStore.ClearSessionAsync(CancellationToken.None);
                return (RefreshOutcome.SignedOut(), true);
            }

            // 5xx / 408 / 429 are temporary: keep the session so a weak-signal moment does not sign the user out.
            return (RefreshOutcome.Failed(error), false);
        }
    }

    private static bool IsDefinitiveRejection(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code is >= 400 and < 500 and not (408 or 429);
    }
}