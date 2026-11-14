using System.Net;
using System.Net.Http.Headers;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class AuthenticationHeaderHandlerTests
{
    [Test]
    public async Task SendAsync_WhenTokenExists_ShouldAddBearerHeader()
    {
        var sessionService = new FakeSessionService { AccessToken = "initial-jwt-token" };
        AuthenticationHeaderValue? capturedAuthHeader = null;

        var innerHandler = new TestHttpMessageHandler((req, _) =>
        {
            capturedAuthHeader = req.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var authHandler = new AuthenticationHeaderHandler(sessionService, innerHandler);
        using var client = new HttpClient(authHandler);

        var response = await client.GetAsync("https://example.com/api/v1/protected");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(capturedAuthHeader, Is.Not.Null);
        Assert.That(capturedAuthHeader!.Scheme, Is.EqualTo("Bearer"));
        Assert.That(capturedAuthHeader.Parameter, Is.EqualTo("initial-jwt-token"));
    }

    [Test]
    public async Task SendAsync_WhenNoTokenExists_ShouldNotAddBearerHeader()
    {
        var sessionService = new FakeSessionService { AccessToken = null };
        AuthenticationHeaderValue? capturedAuthHeader = null;

        var innerHandler = new TestHttpMessageHandler((req, _) =>
        {
            capturedAuthHeader = req.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var authHandler = new AuthenticationHeaderHandler(sessionService, innerHandler);
        using var client = new HttpClient(authHandler);

        var response = await client.GetAsync("https://example.com/api/v1/public");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(capturedAuthHeader, Is.Null);
    }

    [Test]
    public async Task SendAsync_WhenReceives401_ShouldRefreshAndRetryRequestWithNewToken()
    {
        var sessionService = new FakeSessionService
        {
            AccessToken = "expired-token",
            NewTokenOnRefresh = "new-fresh-token"
        };

        var callCount = 0;
        var capturedHeaders = new List<string?>();

        var innerHandler = new TestHttpMessageHandler((req, _) =>
        {
            callCount++;
            capturedHeaders.Add(req.Headers.Authorization?.Parameter);

            if (callCount == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var authHandler = new AuthenticationHeaderHandler(sessionService, innerHandler);
        using var client = new HttpClient(authHandler);

        var response = await client.GetAsync("https://example.com/api/v1/protected");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(callCount, Is.EqualTo(2));
        Assert.That(capturedHeaders[0], Is.EqualTo("expired-token"));
        Assert.That(capturedHeaders[1], Is.EqualTo("new-fresh-token"));
        Assert.That(sessionService.RefreshCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task SendAsync_WhenReceives401AndRefreshFails_ShouldReturn401()
    {
        var sessionService = new FakeSessionService
        {
            AccessToken = "expired-token",
            RefreshSucceeds = false
        };

        var callCount = 0;
        var innerHandler = new TestHttpMessageHandler((_, _) =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        });

        var authHandler = new AuthenticationHeaderHandler(sessionService, innerHandler);
        using var client = new HttpClient(authHandler);

        var response = await client.GetAsync("https://example.com/api/v1/protected");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(callCount, Is.EqualTo(1));
        Assert.That(sessionService.RefreshCalls, Is.EqualTo(1));
    }

    private sealed class FakeSessionService : IAuthenticationSessionService
    {
        public string? AccessToken { get; set; }
        public string? NewTokenOnRefresh { get; set; }
        public bool RefreshSucceeds { get; set; } = true;
        public int RefreshCalls { get; private set; }

        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);
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
            Task.FromResult<string?>("refresh-token");

        public Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(IsAuthenticated);

        public Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            if (!RefreshSucceeds)
            {
                AccessToken = null;
                return Task.FromResult(false);
            }

            AccessToken = NewTokenOnRefresh;
            return Task.FromResult(true);
        }

        public Task ClearSessionAsync(CancellationToken cancellationToken = default)
        {
            AccessToken = null;
            return Task.CompletedTask;
        }
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public TestHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }
}
