using System.Text;
using System.Text.Json;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Infrastructure.Services;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class AuthenticationSessionServiceTests
{
    private InMemorySecureStorageService _storage = null!;
    private StubAuthPort _authPort = null!;
    private AuthenticationSessionService _service = null!;

    [SetUp]
    public void Setup()
    {
        _storage = new InMemorySecureStorageService();
        _authPort = new StubAuthPort();
        _service = new AuthenticationSessionService(_storage, _authPort);
    }

    [Test]
    public async Task SaveSessionAsync_ShouldPersistTokensAndUserToStorage()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@example.com", "STUDENT", "LOCAL", true);

        await _service.SaveSessionAsync("valid-access-token", "valid-refresh-token", user);

        Assert.That(_service.IsAuthenticated, Is.True);
        Assert.That(_service.CurrentUser, Is.EqualTo(user));
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.AccessTokenKey), Is.EqualTo("valid-access-token"));
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.EqualTo("valid-refresh-token"));

        var savedUserJson = await _storage.GetAsync(AuthenticationSessionService.UserDataKey);
        Assert.That(savedUserJson, Is.Not.Null);
        var deserialized = JsonSerializer.Deserialize<AuthUser>(savedUserJson!);
        Assert.That(deserialized?.Email, Is.EqualTo("student@example.com"));
    }

    [Test]
    public void SaveSessionAsync_WhenStorageThrows_ShouldRollbackInMemoryStateAndThrow()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@example.com", "STUDENT", "LOCAL", true);
        _storage.ThrowOnSet = true;
        var eventFired = false;
        _service.CurrentUserChanged += (_, _) => eventFired = true;

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.SaveSessionAsync("valid-access-token", "valid-refresh-token", user));

        Assert.That(ex?.Message, Is.EqualTo("SecureStorage error"));
        Assert.That(_service.IsAuthenticated, Is.False);
        Assert.That(_service.CurrentUser, Is.Null);
        Assert.That(eventFired, Is.False);
    }

    [Test]
    public async Task SaveSessionAsync_WhenPreviousSessionExistsAndStorageThrows_ShouldRollbackToPreviousSession()
    {
        var initialUser = new AuthUser(Guid.NewGuid(), "initial@example.com", "STUDENT", "LOCAL", true);
        await _service.SaveSessionAsync("initial-access", "initial-refresh", initialUser);

        _storage.ThrowOnSet = true;
        var newUser = new AuthUser(Guid.NewGuid(), "new@example.com", "LANDLORD", "LOCAL", true);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.SaveSessionAsync("new-access", "new-refresh", newUser));

        Assert.That(_service.IsAuthenticated, Is.True);
        Assert.That(_service.CurrentUser, Is.EqualTo(initialUser));
        Assert.That(await _service.GetAccessTokenAsync(), Is.EqualTo("initial-access"));
        Assert.That(await _service.GetRefreshTokenAsync(), Is.EqualTo("initial-refresh"));
    }

    [Test]
    public async Task SaveSessionAsync_WhenNewSessionHasNoRefreshToken_ShouldRemovePreviousRefreshToken()
    {
        var userA = new AuthUser(Guid.NewGuid(), "userA@example.com", "STUDENT", "LOCAL", true);
        await _service.SaveSessionAsync("token-A", "refresh-token-A", userA);

        Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.EqualTo("refresh-token-A"));

        // Account B logs in without a refresh token
        var userB = new AuthUser(Guid.NewGuid(), "userB@example.com", "LANDLORD", "GOOGLE", true);
        await _service.SaveSessionAsync("token-B", string.Empty, userB);

        Assert.That(_service.CurrentUser?.Email, Is.EqualTo("userB@example.com"));
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.AccessTokenKey), Is.EqualTo("token-B"));
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.Null);
        Assert.That(await _service.GetRefreshTokenAsync(), Is.Null);
    }

    [Test]
    public async Task TryRestoreSessionAsync_WhenAccessTokenIsValid_ShouldRestoreSessionWithoutCallingBackend()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@example.com", "STUDENT", "LOCAL", true);
        var validJwt = CreateTestJwt(DateTimeOffset.UtcNow.AddHours(1));

        await _storage.SetAsync(AuthenticationSessionService.AccessTokenKey, validJwt);
        await _storage.SetAsync(AuthenticationSessionService.RefreshTokenKey, "raw-refresh-token");
        await _storage.SetAsync(AuthenticationSessionService.UserDataKey, JsonSerializer.Serialize(user));

        var restored = await _service.TryRestoreSessionAsync();

        Assert.That(restored, Is.True);
        Assert.That(_service.IsAuthenticated, Is.True);
        Assert.That(_service.CurrentUser?.Email, Is.EqualTo("student@example.com"));
        Assert.That(_authPort.RefreshTokenCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task TryRestoreSessionAsync_WhenAccessTokenIsExpired_ShouldRefreshAndRotateTokens()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@example.com", "STUDENT", "LOCAL", true);
        var expiredJwt = CreateTestJwt(DateTimeOffset.UtcNow.AddHours(-1));
        var newValidJwt = CreateTestJwt(DateTimeOffset.UtcNow.AddHours(2));

        await _storage.SetAsync(AuthenticationSessionService.AccessTokenKey, expiredJwt);
        await _storage.SetAsync(AuthenticationSessionService.RefreshTokenKey, "old-refresh-token");
        await _storage.SetAsync(AuthenticationSessionService.UserDataKey, JsonSerializer.Serialize(user));

        _authPort.RefreshResult = AuthResult.Success(newValidJwt, "rotated-refresh-token", user);

        var restored = await _service.TryRestoreSessionAsync();

        Assert.That(restored, Is.True);
        Assert.That(_service.IsAuthenticated, Is.True);
        Assert.That(_authPort.RefreshTokenCalls, Is.EqualTo(1));
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.AccessTokenKey), Is.EqualTo(newValidJwt));
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.EqualTo("rotated-refresh-token"));
    }

    [Test]
    public async Task TryRestoreSessionAsync_WhenUserDataIsCorrupted_ShouldClearStorageAndReturnFalse()
    {
        await _storage.SetAsync(AuthenticationSessionService.AccessTokenKey, "some-token");
        await _storage.SetAsync(AuthenticationSessionService.RefreshTokenKey, "some-refresh");
        await _storage.SetAsync(AuthenticationSessionService.UserDataKey, "{ invalid-json }");

        var restored = await _service.TryRestoreSessionAsync();

        Assert.That(restored, Is.False);
        Assert.That(_service.IsAuthenticated, Is.False);
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.AccessTokenKey), Is.Null);
    }

    [Test]
    public async Task TryRestoreSessionAsync_WhenStorageIsEmpty_ShouldReturnFalse()
    {
        var restored = await _service.TryRestoreSessionAsync();

        Assert.That(restored, Is.False);
        Assert.That(_service.IsAuthenticated, Is.False);
    }

    [Test]
    public async Task ClearSessionAsync_ShouldRemoveAllStoredDataAndResetProperties()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@example.com", "STUDENT", "LOCAL", true);
        await _service.SaveSessionAsync("token", "refresh", user);

        await _service.ClearSessionAsync();

        Assert.That(_service.IsAuthenticated, Is.False);
        Assert.That(_service.CurrentUser, Is.Null);
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.AccessTokenKey), Is.Null);
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.Null);
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.UserDataKey), Is.Null);
    }

    [Test]
    public async Task RefreshTokenAsync_WhenTokenIsExpiredOrRevoked_ShouldClearSession()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@example.com", "STUDENT", "LOCAL", true);
        await _service.SaveSessionAsync("token", "revoked-refresh", user);

        _authPort.RefreshResult = AuthResult.InvalidToken("Refresh token expired or revoked.");

        var refreshed = await _service.RefreshTokenAsync();

        Assert.That(refreshed, Is.False);
        Assert.That(_service.IsAuthenticated, Is.False);
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.AccessTokenKey), Is.Null);
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.Null);
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.UserDataKey), Is.Null);
    }

    [Test]
    public async Task RefreshTokenAsync_WhenNetworkFails_ShouldKeepStoredTokensAndUserData()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@example.com", "STUDENT", "LOCAL", true);
        await _service.SaveSessionAsync("current-access", "keep-this-refresh", user);

        _authPort.RefreshResult = AuthResult.NetworkFailure("Unable to connect to authentication server.");

        var refreshed = await _service.RefreshTokenAsync();

        Assert.That(refreshed, Is.False);
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.EqualTo("keep-this-refresh"));
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.UserDataKey), Is.Not.Null);
    }

    [Test]
    public async Task RefreshTokenAsync_WhenTimeout_ShouldKeepStoredTokensAndUserData()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@example.com", "STUDENT", "LOCAL", true);
        await _service.SaveSessionAsync("current-access", "keep-this-refresh", user);

        _authPort.RefreshResult = AuthResult.TimeoutFailure("Connection timed out.");

        var refreshed = await _service.RefreshTokenAsync();

        Assert.That(refreshed, Is.False);
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.EqualTo("keep-this-refresh"));
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.UserDataKey), Is.Not.Null);
    }

    [Test]
    public async Task RefreshTokenAsync_WhenServer500Error_ShouldKeepStoredTokensAndUserData()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@example.com", "STUDENT", "LOCAL", true);
        await _service.SaveSessionAsync("current-access", "keep-this-refresh", user);

        _authPort.RefreshResult = AuthResult.ServerFailure("Internal server error.");

        var refreshed = await _service.RefreshTokenAsync();

        Assert.That(refreshed, Is.False);
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.EqualTo("keep-this-refresh"));
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.UserDataKey), Is.Not.Null);
    }

    [Test]
    public async Task RefreshTokenAsync_WhenBadRequestValidation_ShouldKeepStoredTokensAndUserData()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@example.com", "STUDENT", "LOCAL", true);
        await _service.SaveSessionAsync("current-access", "keep-this-refresh", user);

        _authPort.RefreshResult = AuthResult.BadRequest("Validation Failed: Refresh token is required.");

        var refreshed = await _service.RefreshTokenAsync();

        Assert.That(refreshed, Is.False);
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.EqualTo("keep-this-refresh"));
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.UserDataKey), Is.Not.Null);
    }

    [Test]
    public async Task RefreshTokenAsync_WhenForbidden_ShouldKeepStoredTokensAndUserData()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@example.com", "STUDENT", "LOCAL", true);
        await _service.SaveSessionAsync("current-access", "keep-this-refresh", user);

        _authPort.RefreshResult = AuthResult.Forbidden("Forbidden: Not allowed.");

        var refreshed = await _service.RefreshTokenAsync();

        Assert.That(refreshed, Is.False);
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.EqualTo("keep-this-refresh"));
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.UserDataKey), Is.Not.Null);
    }

    [Test]
    public async Task TryRestoreSessionAsync_WhenAccessTokenExpiredAndNetworkFails_ShouldPreserveStoredSession()
    {
        var expiredJwt = CreateTestJwt(DateTimeOffset.UtcNow.AddMinutes(-10));
        var user = new AuthUser(Guid.NewGuid(), "offline@example.com", "STUDENT", "LOCAL", true);
        await _storage.SetAsync(AuthenticationSessionService.AccessTokenKey, expiredJwt);
        await _storage.SetAsync(AuthenticationSessionService.RefreshTokenKey, "offline-refresh-token");
        await _storage.SetAsync(AuthenticationSessionService.UserDataKey, JsonSerializer.Serialize(user));

        _authPort.RefreshResult = AuthResult.NetworkFailure("Offline / no internet connection.");

        var restored = await _service.TryRestoreSessionAsync();

        Assert.That(restored, Is.False);
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.EqualTo("offline-refresh-token"));
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.UserDataKey), Is.Not.Null);
    }

    [Test]
    public async Task TryRestoreSessionAsync_WhenAccessTokenExpiredAndTokenRevoked_ShouldClearStoredSession()
    {
        var expiredJwt = CreateTestJwt(DateTimeOffset.UtcNow.AddMinutes(-10));
        var user = new AuthUser(Guid.NewGuid(), "revoked@example.com", "STUDENT", "LOCAL", true);
        await _storage.SetAsync(AuthenticationSessionService.AccessTokenKey, expiredJwt);
        await _storage.SetAsync(AuthenticationSessionService.RefreshTokenKey, "revoked-refresh-token");
        await _storage.SetAsync(AuthenticationSessionService.UserDataKey, JsonSerializer.Serialize(user));

        _authPort.RefreshResult = AuthResult.InvalidToken("invalid_grant: refresh token revoked");

        var restored = await _service.TryRestoreSessionAsync();

        Assert.That(restored, Is.False);
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.RefreshTokenKey), Is.Null);
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.UserDataKey), Is.Null);
    }

    private static string CreateTestJwt(DateTimeOffset expiresAt)
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"alg":"HS256","typ":"JWT"}"""));
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes($$"""{"sub":"test","exp":{{expiresAt.ToUnixTimeSeconds()}}}"""));
        var signature = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-signature"));
        return $"{header}.{payload}.{signature}";
    }

    private sealed class InMemorySecureStorageService : ISecureStorageService
    {
        private readonly Dictionary<string, string> _dict = new();
        public bool ThrowOnSet { get; set; }

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(_dict.TryGetValue(key, out var val) ? val : null);

        public Task SetAsync(string key, string value)
        {
            if (ThrowOnSet)
            {
                throw new InvalidOperationException("SecureStorage error");
            }

            _dict[key] = value;
            return Task.CompletedTask;
        }

        public bool Remove(string key) => _dict.Remove(key);

        public void RemoveAll() => _dict.Clear();
    }

    private sealed class StubAuthPort : IAuthenticationPort
    {
        public int RefreshTokenCalls { get; private set; }
        public AuthResult RefreshResult { get; set; } = AuthResult.Success("", "", new AuthUser(Guid.NewGuid(), "", "", "", true));

        public Task<AuthResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> LoginWithEmailAsync(LoginWithEmailRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> LoginWithGoogleAsync(LoginWithGoogleRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            RefreshTokenCalls++;
            return Task.FromResult(RefreshResult);
        }

        public Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
