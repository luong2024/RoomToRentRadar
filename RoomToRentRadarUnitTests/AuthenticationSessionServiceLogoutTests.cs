using System.Text.Json;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Infrastructure.Services;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class AuthenticationSessionServiceLogoutTests
{
    private InMemoryStorage _storage = null!;
    private StubAuthPort _authPort = null!;
    private AuthenticationSessionService _service = null!;

    [SetUp]
    public void Setup()
    {
        _storage = new InMemoryStorage();
        _authPort = new StubAuthPort();
        _service = new AuthenticationSessionService(_storage, _authPort);
    }

    [Test]
    public async Task LogoutAsync_WhenBackendRevocationSucceeds_ShouldClearSessionAndReturnSuccess()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@test.com", "STUDENT", "LOCAL", true);
        await _service.SaveSessionAsync("access-123", "refresh-123", user);

        _authPort.LogoutReturnValue = true;
        var eventFiredWithNull = false;
        _service.CurrentUserChanged += (_, currentUser) =>
        {
            if (currentUser == null) eventFiredWithNull = true;
        };

        var result = await _service.LogoutAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(result.LocalSessionCleared, Is.True);
            Assert.That(result.ServerRevoked, Is.True);
            Assert.That(result.ErrorMessage, Is.Null);

            Assert.That(_service.IsAuthenticated, Is.False);
            Assert.That(_service.CurrentUser, Is.Null);
            Assert.That(await _service.GetAccessTokenAsync(), Is.Null);
            Assert.That(await _service.GetRefreshTokenAsync(), Is.Null);

            Assert.That(await _storage.GetAsync(AuthenticationSessionService.AccessTokenKey), Is.Null);
            Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.Null);
            Assert.That(await _storage.GetAsync(AuthenticationSessionService.UserDataKey), Is.Null);

            Assert.That(_authPort.LastLogoutRefreshToken, Is.EqualTo("refresh-123"));
            Assert.That(eventFiredWithNull, Is.True);
        });
    }

    [Test]
    public async Task LogoutAsync_WhenBackendThrowsNetworkException_ShouldStillClearLocalSession()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@test.com", "STUDENT", "LOCAL", true);
        await _service.SaveSessionAsync("access-123", "refresh-123", user);

        _authPort.ThrowOnLogout = new HttpRequestException("Server unreachable / offline");

        var result = await _service.LogoutAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(result.LocalSessionCleared, Is.True);
            Assert.That(result.ServerRevoked, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Server unreachable"));

            Assert.That(_service.IsAuthenticated, Is.False);
            Assert.That(_service.CurrentUser, Is.Null);
            Assert.That(await _storage.GetAsync(AuthenticationSessionService.AccessTokenKey), Is.Null);
            Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.Null);
            Assert.That(await _storage.GetAsync(AuthenticationSessionService.UserDataKey), Is.Null);
        });
    }

    [Test]
    public async Task LogoutAsync_WhenBackendReturnsFalse_ShouldStillClearLocalSession()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@test.com", "STUDENT", "LOCAL", true);
        await _service.SaveSessionAsync("access-123", "refresh-123", user);

        _authPort.LogoutReturnValue = false;

        var result = await _service.LogoutAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(result.LocalSessionCleared, Is.True);
            Assert.That(result.ServerRevoked, Is.False);
            Assert.That(_service.IsAuthenticated, Is.False);
            Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.Null);
        });
    }

    [Test]
    public async Task LogoutAsync_WhenCalledRepeatedly_ShouldBeIdempotentAndSafe()
    {
        var firstResult = await _service.LogoutAsync();
        var secondResult = await _service.LogoutAsync();

        Assert.That(firstResult.LocalSessionCleared, Is.True);
        Assert.That(secondResult.LocalSessionCleared, Is.True);
        Assert.That(_service.IsAuthenticated, Is.False);
    }

    [Test]
    public async Task LogoutAsync_WhenSecureStorageThrowsOnRemove_ShouldStillWipeMemoryState()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@test.com", "STUDENT", "LOCAL", true);
        await _service.SaveSessionAsync("access-123", "refresh-123", user);

        _storage.ThrowOnRemove = true;

        var result = await _service.LogoutAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(result.LocalSessionCleared, Is.True);
            Assert.That(_service.IsAuthenticated, Is.False);
            Assert.That(_service.CurrentUser, Is.Null);
            Assert.That(await _service.GetAccessTokenAsync(), Is.Null);
        });
    }

    [Test]
    public async Task RefreshTokenAsync_WhenLogoutOccursWhileRefreshIsInFlight_ShouldDiscardRefreshedSession()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@test.com", "STUDENT", "LOCAL", true);
        await _service.SaveSessionAsync("access-old", "refresh-old", user);

        var refreshStartedTcs = new TaskCompletionSource<bool>();
        var allowRefreshToCompleteTcs = new TaskCompletionSource<bool>();

        _authPort.OnRefreshTokenAsync = async () =>
        {
            refreshStartedTcs.TrySetResult(true);
            await allowRefreshToCompleteTcs.Task;
            return AuthResult.Success("access-NEW", "refresh-NEW", user);
        };

        // 1. Start Refresh in background
        var refreshTask = Task.Run(() => _service.RefreshTokenAsync());
        await refreshStartedTcs.Task;

        // 2. User logs out while refresh is still awaiting backend response
        await _service.LogoutAsync();

        // 3. Allow backend refresh response to return
        allowRefreshToCompleteTcs.TrySetResult(true);
        var refreshSuccess = await refreshTask;

        // 4. Verification: The refresh operation MUST NOT resurrect the session
        Assert.Multiple(async () =>
        {
            Assert.That(refreshSuccess, Is.False, "In-flight refresh must be rejected if logout occurred.");
            Assert.That(_service.IsAuthenticated, Is.False);
            Assert.That(_service.CurrentUser, Is.Null);
            Assert.That(await _service.GetAccessTokenAsync(), Is.Null);
            Assert.That(await _storage.GetAsync(AuthenticationSessionService.AccessTokenKey), Is.Null);
        });
    }

    [Test]
    public async Task RefreshTokenAsync_WhenServerReturnsUnauthorizedInvalidCredentials_ShouldClearSessionAndNotify()
    {
        var user = new AuthUser(Guid.NewGuid(), "expired@test.com", "STUDENT", "LOCAL", true);
        await _service.SaveSessionAsync("access-old", "refresh-expired", user);

        _authPort.OnRefreshTokenAsync = () =>
            Task.FromResult(AuthResult.Failed("Unauthorized", AuthFailureType.InvalidCredentials));

        var notifiedNull = false;
        _service.CurrentUserChanged += (_, currentUser) =>
        {
            if (currentUser == null) notifiedNull = true;
        };

        var refreshed = await _service.RefreshTokenAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(refreshed, Is.False);
            Assert.That(_service.IsAuthenticated, Is.False);
            Assert.That(_service.CurrentUser, Is.Null);
            Assert.That(notifiedNull, Is.True);
            Assert.That(await _storage.GetAsync(AuthenticationSessionService.AccessTokenKey), Is.Null);
            Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.Null);
            Assert.That(await _storage.GetAsync(AuthenticationSessionService.UserDataKey), Is.Null);
        });
    }

    [Test]
    public async Task EnsureInitializedAsync_ShouldAwaitStartupRestoration()
    {
        var user = new AuthUser(Guid.NewGuid(), "init@test.com", "STUDENT", "LOCAL", true);
        var jwt = CreateValidJwt();
        await _storage.SetAsync(AuthenticationSessionService.AccessTokenKey, jwt);
        await _storage.SetAsync(AuthenticationSessionService.RefreshTokenKey, "raw-refresh");
        await _storage.SetAsync(AuthenticationSessionService.UserDataKey, JsonSerializer.Serialize(user));

        Assert.That(_service.IsInitialized, Is.False);

        var restoreTask = Task.Run(async () =>
        {
            await Task.Delay(50);
            return await _service.TryRestoreSessionAsync();
        });

        await _service.EnsureInitializedAsync();
        var restored = await restoreTask;

        Assert.Multiple(() =>
        {
            Assert.That(_service.IsInitialized, Is.True);
            Assert.That(restored, Is.True);
            Assert.That(_service.IsAuthenticated, Is.True);
            Assert.That(_service.CurrentUser?.Email, Is.EqualTo("init@test.com"));
        });
    }

    private static string CreateValidJwt()
    {
        var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var header = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("""{"alg":"HS256","typ":"JWT"}"""));
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($$"""{"sub":"123","exp":{{exp}}}"""));
        return $"{header}.{payload}.sig";
    }

    private sealed class InMemoryStorage : ISecureStorageService
    {
        private readonly Dictionary<string, string> _dict = new();
        public bool ThrowOnRemove { get; set; }

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(_dict.TryGetValue(key, out var val) ? val : null);

        public Task SetAsync(string key, string value)
        {
            _dict[key] = value;
            return Task.CompletedTask;
        }

        public bool Remove(string key)
        {
            if (ThrowOnRemove) throw new InvalidOperationException("Storage remove failed");
            return _dict.Remove(key);
        }

        public void RemoveAll()
        {
            if (ThrowOnRemove) throw new InvalidOperationException("Storage remove failed");
            _dict.Clear();
        }
    }

    private sealed class StubAuthPort : IAuthenticationPort
    {
        public bool LogoutReturnValue { get; set; } = true;
        public Exception? ThrowOnLogout { get; set; }
        public string? LastLogoutRefreshToken { get; private set; }
        public Func<Task<AuthResult>>? OnRefreshTokenAsync { get; set; }

        public Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            LastLogoutRefreshToken = refreshToken;
            if (ThrowOnLogout != null) throw ThrowOnLogout;
            return Task.FromResult(LogoutReturnValue);
        }

        public async Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            if (OnRefreshTokenAsync != null)
            {
                return await OnRefreshTokenAsync();
            }

            return AuthResult.Success("fresh-acc", "fresh-ref", new AuthUser(Guid.NewGuid(), "a@b.com", "STUDENT", "LOCAL", true));
        }

        public Task<AuthResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> LoginWithEmailAsync(LoginWithEmailRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> LoginWithGoogleAsync(LoginWithGoogleRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
