using System.Collections.Concurrent;
using System.Net;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Infrastructure.Services;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class AuthenticationHeaderHandlerAccountIsolationTests
{
    [Test]
    public async Task SendAsync_WhenAccountSwitchesWhileRequestInFlight_ShouldNotRetryWithNewAccountToken()
    {
        var userA = new AuthUser(Guid.NewGuid(), "userA@test.com", "STUDENT", "LOCAL", true);
        var userB = new AuthUser(Guid.NewGuid(), "userB@test.com", "LANDLORD", "LOCAL", true);

        var sessionService = new IsolationFakeSessionService();
        await sessionService.SaveSessionAsync("account-A-token", "refresh-A", userA);

        var callCount = 0;
        var innerHandler = new DelegatingTestHandler(async (_, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                // Simulate account switch during in-flight execution:
                // Account A is replaced by Account B logging in
                await sessionService.SaveSessionAsync("account-B-token", "refresh-B", userB);
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
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
    public async Task SendAsync_WhenTwoConcurrentRequestsReceive401_FirstRefreshesAndSecondRetriesWithNewToken()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@test.com", "STUDENT", "LOCAL", true);
        var storage = new InMemoryStorage();
        var authPort = new StubAuthPort();
        var sessionService = new AuthenticationSessionService(storage, authPort);

        await sessionService.SaveSessionAsync("expired-access-token", "initial-refresh-token", user);

        var bothRequestsSentInitial401Tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var initialRequestCount = 0;
        var retryTokensUsed = new ConcurrentBag<string?>();

        authPort.OnRefreshTokenAsync = async _ =>
        {
            await Task.Delay(25);
            return AuthResult.Success("rotated-access-token", "rotated-refresh-token", user);
        };

        var innerHandler = new DelegatingTestHandler(async (req, _) =>
        {
            var bearerToken = req.Headers.Authorization?.Parameter;
            if (bearerToken == "expired-access-token")
            {
                if (Interlocked.Increment(ref initialRequestCount) == 2)
                {
                    bothRequestsSentInitial401Tcs.TrySetResult(true);
                }

                await bothRequestsSentInitial401Tcs.Task;
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            retryTokensUsed.Add(bearerToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = new AuthenticationHeaderHandler(sessionService, innerHandler);
        using var client = new HttpClient(handler);

        var requestATask = client.GetAsync("https://example.com/api/v1/rooms?page=1");
        var requestBTask = client.GetAsync("https://example.com/api/v1/rooms?page=2");

        var responses = await Task.WhenAll(requestATask, requestBTask);

        Assert.Multiple(() =>
        {
            Assert.That(responses[0].StatusCode, Is.EqualTo(HttpStatusCode.OK), "First request must succeed after refreshing token.");
            Assert.That(responses[1].StatusCode, Is.EqualTo(HttpStatusCode.OK), "Second concurrent request must reuse refreshed token and succeed.");
            Assert.That(authPort.RefreshCallCount, Is.EqualTo(1), "Backend refresh endpoint must only be called once for both concurrent 401 requests.");
            Assert.That(retryTokensUsed.ToArray(), Is.EqualTo(new[] { "rotated-access-token", "rotated-access-token" }));
        });
    }

    [Test]
    public async Task SendAsync_WhenSecondRequestReceives401AfterFirstRequestAlreadyRotatedToken_ShouldReuseNewTokenWithoutSecondRefresh()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@test.com", "STUDENT", "LOCAL", true);
        var storage = new InMemoryStorage();
        var authPort = new StubAuthPort();
        var sessionService = new AuthenticationSessionService(storage, authPort);

        await sessionService.SaveSessionAsync("expired-access-token", "initial-refresh-token", user);

        var requestBEnteredInFlightTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestACompletedRefreshAndRetryTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var retryTokensUsed = new ConcurrentQueue<string?>();

        authPort.OnRefreshTokenAsync = _ =>
            Task.FromResult(AuthResult.Success("rotated-access-token", "rotated-refresh-token", user));

        var innerHandler = new DelegatingTestHandler(async (req, _) =>
        {
            var bearerToken = req.Headers.Authorization?.Parameter;
            var pathAndQuery = req.RequestUri?.PathAndQuery ?? string.Empty;

            if (bearerToken == "expired-access-token")
            {
                if (pathAndQuery.Contains("request=B", StringComparison.Ordinal))
                {
                    requestBEnteredInFlightTcs.TrySetResult(true);
                    // Wait until Request A has completely finished refreshing and retrying
                    // so currentToken in sessionService is ALREADY "rotated-access-token" when Request B gets its 401
                    await requestACompletedRefreshAndRetryTcs.Task;
                }

                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            retryTokensUsed.Enqueue(bearerToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = new AuthenticationHeaderHandler(sessionService, innerHandler);
        using var client = new HttpClient(handler);

        // Start Request B first so it captures "expired-access-token" and stays in-flight
        var requestBTask = client.GetAsync("https://example.com/api/v1/rooms?request=B");
        await requestBEnteredInFlightTcs.Task;

        // Run Request A to completion (receives 401 -> refreshes token -> retries with "rotated-access-token" -> 200 OK)
        var responseA = await client.GetAsync("https://example.com/api/v1/rooms?request=A");
        requestACompletedRefreshAndRetryTcs.TrySetResult(true);

        // Now Request B receives its 401 after currentToken has already changed to "rotated-access-token"
        var responseB = await requestBTask;

        Assert.Multiple(() =>
        {
            Assert.That(responseA.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(responseB.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Request B must detect same-session token rotation and retry with rotated token.");
            Assert.That(authPort.RefreshCallCount, Is.EqualTo(1), "Request B must reuse Request A's rotated token without calling backend refresh again.");
            Assert.That(retryTokensUsed.ToArray(), Is.EqualTo(new[] { "rotated-access-token", "rotated-access-token" }));
        });
    }

    [Test]
    public async Task SendAsync_WhenAuthEndpointReturns401_ShouldNotTriggerRecursiveRefresh()
    {
        var sessionService = new IsolationFakeSessionService();
        await sessionService.SaveSessionAsync(
            "some-token",
            "some-refresh",
            new AuthUser(Guid.NewGuid(), "user@test.com", "STUDENT", "LOCAL", true));

        var callCount = 0;
        var innerHandler = new DelegatingTestHandler((_, _) =>
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
        private long _sessionEpoch;
        public string? AccessToken { get; set; }
        public int RefreshCalls { get; private set; }

        public bool IsAuthenticated => CurrentUser != null && !string.IsNullOrEmpty(AccessToken);
        public bool IsInitialized => true;
        public AuthUser? CurrentUser { get; private set; }
        public long SessionEpoch => Interlocked.Read(ref _sessionEpoch);
#pragma warning disable CS0067
        public event EventHandler<AuthUser?>? CurrentUserChanged;
#pragma warning restore CS0067

        public Task SaveSessionAsync(string accessToken, string refreshToken, AuthUser user, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _sessionEpoch);
            AccessToken = accessToken;
            CurrentUser = user;
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
            Interlocked.Increment(ref _sessionEpoch);
            AccessToken = null;
            CurrentUser = null;
            return Task.CompletedTask;
        }

        public Task<LogoutResult> LogoutAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _sessionEpoch);
            AccessToken = null;
            CurrentUser = null;
            return Task.FromResult(new LogoutResult(true, true));
        }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class InMemoryStorage : ISecureStorageService
    {
        private readonly ConcurrentDictionary<string, string> _dict = new();

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(_dict.TryGetValue(key, out var val) ? val : null);

        public Task SetAsync(string key, string value)
        {
            _dict[key] = value;
            return Task.CompletedTask;
        }

        public bool Remove(string key) => _dict.TryRemove(key, out _);

        public void RemoveAll() => _dict.Clear();
    }

    private sealed class StubAuthPort : IAuthenticationPort
    {
        private int _refreshCallCount;
        public int RefreshCallCount => Volatile.Read(ref _refreshCallCount);
        public Func<string, Task<AuthResult>>? OnRefreshTokenAsync { get; set; }

        public async Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _refreshCallCount);
            if (OnRefreshTokenAsync != null)
            {
                return await OnRefreshTokenAsync(refreshToken);
            }

            return AuthResult.Success("rotated-access", "rotated-refresh", new AuthUser(Guid.NewGuid(), "a@b.com", "STUDENT", "LOCAL", true));
        }

        public Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<AuthResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> LoginWithEmailAsync(LoginWithEmailRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> LoginWithGoogleAsync(LoginWithGoogleRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
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
