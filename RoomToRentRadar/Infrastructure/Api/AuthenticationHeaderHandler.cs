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

        // If unauthorized and we had a token, try refreshing and retrying once
        if (response.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrWhiteSpace(token))
        {
            var refreshed = await _sessionService.RefreshTokenAsync(cancellationToken);
            if (refreshed)
            {
                var newToken = await _sessionService.GetAccessTokenAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(newToken))
                {
                    using var clonedRequest = await CloneHttpRequestMessageAsync(request, cancellationToken);
                    clonedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);

                    response.Dispose();
                    return await base.SendAsync(clonedRequest, cancellationToken);
                }
            }
        }

        return response;
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        if (request.Content != null)
        {
            var ms = new MemoryStream();
            await request.Content.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
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
