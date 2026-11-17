using System.Net;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class AuthenticationHeaderHandlerAccountIsolationTests
{
    [Test]
    public async Task SendAsync_WhenAccountSwitchesWhileRequestInFlight_ShouldNotRetryWithNewAccountToken()
    {
        var sessionService = new IsolationFakeSessionService
        {
            AccessToken = "account-A-token"
        };

        var callCount = 0;
        var innerHandler = new DelegatingTestHandler((req, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                // Simulate account switch during in-flight execution:
                // Account A is logged out and Account B logs in
                sessionService.AccessToken = "account-B-token";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var handler = new AuthenticationHeaderHandler(sessionService, innerHandler);
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("https://example.com/api/v1/rooms");

        Assert.Multiple(() =>
        {
            // Must NOT retry with Account B's token: returns the 401 response directly
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(callCount, Is.EqualTo(1));
            Assert.That(sessionService.RefreshCalls, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task SendAsync_WhenAuthEndpointReturns401_ShouldNotTriggerRecursiveRefresh()
    {
        var sessionService = new IsolationFakeSessionService
        {
            AccessToken = "some-token"
        };

        var callCount = 0;
        var innerHandler = new DelegatingTestHandler((req, _) =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        });

        var handler = new AuthenticationHeaderHandler(sessionService, innerHandler);
        using var client = new HttpClient(handler);

        var response = await client.PostAsync("https://example.com/api/v1/auth/refresh", new StringContent("{}"));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(callCount, Is.EqualTo(1));
            Assert.That(sessionService.RefreshCalls, Is.EqualTo(0), "Auth endpoints must never trigger auto-refresh.");
        });
    }

    private sealed class IsolationFakeSessionService : IAuthenticationSessionService
    {
        public string? AccessToken { get; set; }
        public int RefreshCalls { get; private set; }

        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);
        public bool IsInitialized => true;
        public AuthUser? CurrentUser => null;
#pragma warning disable CS0067
        public event EventHandler<AuthUser?>? CurrentUserChanged;
#pragma warning restore CS0067

        public Task SaveSessionAsync(string accessToken, string refreshToken, AuthUser user, CancellationToken cancellationToken = default)
        {
            AccessToken = accessToken;
            return Task.CompletedTask;
        }

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(AccessToken);

        public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("sample-refresh");

        public Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(IsAuthenticated);

        public Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            AccessToken = "refreshed-token";
            return Task.FromResult(true);
        }

        public Task ClearSessionAsync(CancellationToken cancellationToken = default)
        {
            AccessToken = null;
            return Task.CompletedTask;
        }

        public Task<LogoutResult> LogoutAsync(CancellationToken cancellationToken = default)
        {
            AccessToken = null;
            return Task.FromResult(new LogoutResult(true, true));
        }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class DelegatingTestHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public DelegatingTestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }
}
