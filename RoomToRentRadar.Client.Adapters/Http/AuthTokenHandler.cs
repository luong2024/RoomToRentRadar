using System.Net;
using System.Net.Http.Headers;
using RoomToRentRadar.Client.Adapters.Abstractions;

namespace RoomToRentRadar.Client.Adapters.Http;

/// <summary>
/// Attaches the session Bearer token and, on 401, refreshes once through the shared
/// <see cref="TokenRefreshCoordinator"/> then replays the request with the new token.
/// Auth endpoints (except /api/auth/me) and requests with a caller-set Authorization header bypass it.
/// </summary>
public sealed class AuthTokenHandler : DelegatingHandler
{
    private readonly ISessionStore _sessionStore;
    private readonly TokenRefreshCoordinator _refreshCoordinator;

    public AuthTokenHandler(ISessionStore sessionStore, TokenRefreshCoordinator refreshCoordinator)
    {
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _refreshCoordinator = refreshCoordinator ?? throw new ArgumentNullException(nameof(refreshCoordinator));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (ShouldBypass(request))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var accessToken = await _sessionStore.GetAccessTokenAsync(cancellationToken);
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        // Buffer the body up front so the retry can replay it byte-for-byte (uploads are capped at 10 MB, BR-MED-02).
        byte[]? body = null;
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync();
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        var outcome = await _refreshCoordinator.RefreshAsync(accessToken, cancellationToken);
        switch (outcome.Kind)
        {
            case RefreshOutcomeKind.Refreshed:
                response.Dispose();
                var retry = CloneRequest(request, body, outcome.AccessToken);
                // At most one retry: a second 401 is returned as-is, without another refresh or sign-out.
                return await base.SendAsync(retry, cancellationToken);

            case RefreshOutcomeKind.Failed:
                response.Dispose();
                throw outcome.Error!;

            default:
                // NoSession (guest) or SignedOut: return the original 401.
                return response;
        }
    }

    private static bool ShouldBypass(HttpRequestMessage request)
    {
        if (request.Headers.Authorization is not null)
        {
            return true;
        }

        var uri = request.RequestUri;
        var path = uri is null ? string.Empty : uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString;
        return path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase)
            && !path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase);
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage original, byte[]? body, string? newAccessToken)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version,
            VersionPolicy = original.VersionPolicy
        };

        foreach (var header in original.Headers)
        {
            if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (body is not null && original.Content is not null)
        {
            var content = new ByteArrayContent(body);
            foreach (var header in original.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = content;
        }

        foreach (var option in original.Options)
        {
            ((IDictionary<string, object?>)clone.Options)[option.Key] = option.Value;
        }

        if (!string.IsNullOrEmpty(newAccessToken))
        {
            clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);
        }

        return clone;
    }
}