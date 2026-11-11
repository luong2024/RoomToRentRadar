using System.Net;
using System.Net.Http.Headers;
using RoomToRentRadar.Core.Interfaces;

namespace RoomToRentRadar.Infrastructure.Auth;

public sealed class AuthenticatedHttpMessageHandler(ITokenStore tokenStore)
    : DelegatingHandler(new HttpClientHandler())
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await tokenStore.GetAccessTokenAsync(cancellationToken);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // An expired or revoked token must not keep being sent on every request.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await tokenStore.ClearAsync(cancellationToken);
        }

        return response;
    }
}
