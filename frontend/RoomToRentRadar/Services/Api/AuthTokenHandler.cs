using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace RoomToRentRadar.Services.Api
{
    /// <summary>
    /// US01 / T01.2 (AC3): attaches "Authorization: Bearer &lt;token&gt;" to every request sent through the
    /// shared API HttpClient, so feature clients (ApiRoomListingRepository, ApiFavoriteRepository...) never
    /// handle tokens themselves. If the API rejects a token we sent (401), the saved session is cleared
    /// so the app shows the user as signed out instead of failing on every call.
    /// </summary>
    public sealed class AuthTokenHandler : DelegatingHandler
    {
        private readonly AuthTokenStore _tokens;
        private readonly ILogger<AuthTokenHandler> _logger;

        public AuthTokenHandler(AuthTokenStore tokens, ILogger<AuthTokenHandler> logger)
        {
            _tokens = tokens;
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await _tokens.LoadAsync();

            var token = _tokens.AccessToken;
            var sentToken = false;

            // Respect a header set explicitly by the caller; otherwise add the saved token.
            if (request.Headers.Authorization is null && !string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                sentToken = true;
            }

            var response = await base.SendAsync(request, cancellationToken);

            // Only a 401 for a request that carried OUR token means the session is invalid
            // (a wrong password on /login also returns 401 but sends no token, so it is ignored here).
            if (sentToken && response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogInformation("API rejected the saved token (401); clearing the session.");
                _tokens.Clear();
            }

            return response;
        }
    }
}
