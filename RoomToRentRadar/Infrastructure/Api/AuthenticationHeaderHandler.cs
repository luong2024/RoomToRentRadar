using System.Net;
using System.Net.Http.Headers;
using RoomToRentRadar.Core.Interfaces;

namespace RoomToRentRadar.Infrastructure.Api;

/// <summary>
/// DelegatingHandler that automatically adds the Bearer access token to outbound HTTP requests
/// and handles 401 Unauthorized responses by refreshing the token and retrying once.
/// </summary>
public class AuthenticationHeaderHandler : DelegatingHandler
{
    private readonly IAuthenticationSessionService _sessionService;

    public AuthenticationHeaderHandler(IAuthenticationSessionService sessionService)
        : this(sessionService, new HttpClientHandler())
    {
    }

    public AuthenticationHeaderHandler(IAuthenticationSessionService sessionService, HttpMessageHandler innerHandler)
    {
        _sessionService = sessionService;
        InnerHandler = innerHandler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var initialSessionEpoch = _sessionService.SessionEpoch;
        var initialUserId = _sessionService.CurrentUser?.Id;
        var token = await _sessionService.GetAccessTokenAsync(cancellationToken);
        initialUserId ??= _sessionService.CurrentUser?.Id;

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // If unauthorized and we had a token, only retry if request body can be safely reproduced
        // and request was not directed to an authentication endpoint (preventing recursion)
        if (response.StatusCode == HttpStatusCode.Unauthorized &&
            !string.IsNullOrWhiteSpace(token) &&
            !IsAuthEndpoint(request.RequestUri) &&
            CanRetryRequest(request))
        {
            // Verify account isolation: do not retry if user logged out or switched accounts while request was in-flight
            if (!IsSameActiveSession(initialUserId, initialSessionEpoch))
            {
                return response;
            }

            var currentToken = await _sessionService.GetAccessTokenAsync(cancellationToken);
            if (!IsSameActiveSession(initialUserId, initialSessionEpoch))
            {
                return response;
            }

            string? retryToken = null;

            // Distinguish same-session token rotation from account switch/logout:
            // If another concurrent request in the same session already rotated the access token,
            // reuse that new token directly; otherwise trigger a token refresh.
            if (!string.IsNullOrWhiteSpace(currentToken) &&
                !string.Equals(currentToken, token, StringComparison.Ordinal))
            {
                retryToken = currentToken;
            }
            else if (await _sessionService.RefreshTokenAsync(cancellationToken) &&
                     IsSameActiveSession(initialUserId, initialSessionEpoch))
            {
                retryToken = await _sessionService.GetAccessTokenAsync(cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(retryToken) &&
                IsSameActiveSession(initialUserId, initialSessionEpoch))
            {
                var clonedRequest = await CloneHttpRequestMessageAsync(request, cancellationToken);
                if (clonedRequest != null)
                {
                    using (clonedRequest)
                    {
                        clonedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", retryToken);
                        response.Dispose();
                        return await base.SendAsync(clonedRequest, cancellationToken);
                    }
                }
            }
        }

        return response;
    }

    private bool IsSameActiveSession(Guid? initialUserId, long initialSessionEpoch)
    {
        if (!_sessionService.IsAuthenticated)
        {
            return false;
        }

        if (_sessionService.SessionEpoch != initialSessionEpoch)
        {
            return false;
        }

        var currentUserId = _sessionService.CurrentUser?.Id;
        return initialUserId == currentUserId;
    }

    private static bool IsAuthEndpoint(Uri? uri)
    {
        if (uri == null)
        {
            return false;
        }

        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.Contains("/api/v1/auth/refresh") ||
               path.Contains("/api/v1/auth/login") ||
               path.Contains("/api/v1/auth/logout") ||
               path.Contains("/api/v1/auth/register") ||
               path.Contains("/api/v1/auth/google");
    }

    private static bool CanRetryRequest(HttpRequestMessage request)
    {
        if (request.Content == null)
        {
            return true;
        }

        try
        {
            var stream = request.Content.ReadAsStream();
            return stream.CanSeek;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<HttpRequestMessage?> CloneHttpRequestMessageAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        if (request.Content != null)
        {
            Stream stream;
            try
            {
                stream = await request.Content.ReadAsStreamAsync(cancellationToken);
            }
            catch
            {
                clone.Dispose();
                return null;
            }

            if (!stream.CanSeek)
            {
                clone.Dispose();
                return null;
            }

            stream.Position = 0;
            var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            stream.Position = 0;
            clone.Content = new StreamContent(ms);

            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var property in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(property.Key), property.Value);
        }

        return clone;
    }
}
