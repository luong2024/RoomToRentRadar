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
        var token = await _sessionService.GetAccessTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // If unauthorized and we had a token, only retry if request body can be safely reproduced
        if (response.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrWhiteSpace(token))
        {
            if (CanRetryRequest(request))
            {
                var refreshed = await _sessionService.RefreshTokenAsync(cancellationToken);
                if (refreshed)
                {
                    var newToken = await _sessionService.GetAccessTokenAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(newToken))
                    {
                        var clonedRequest = await CloneHttpRequestMessageAsync(request, cancellationToken);
                        if (clonedRequest != null)
                        {
                            using (clonedRequest)
                            {
                                clonedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
                                response.Dispose();
                                return await base.SendAsync(clonedRequest, cancellationToken);
                            }
                        }
                    }
                }
            }
        }

        return response;
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
