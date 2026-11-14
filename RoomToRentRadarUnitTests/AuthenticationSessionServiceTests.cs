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
    public async Task RefreshTokenAsync_WhenBackendReturnsFailed_ShouldClearSession()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@example.com", "STUDENT", "LOCAL", true);
        await _service.SaveSessionAsync("token", "revoked-refresh", user);

        _authPort.RefreshResult = AuthResult.Failed("Refresh token expired or revoked.");

        var refreshed = await _service.RefreshTokenAsync();

        Assert.That(refreshed, Is.False);
        Assert.That(_service.IsAuthenticated, Is.False);
        Assert.That(await _storage.GetAsync(AuthenticationSessionService.AccessTokenKey), Is.Null);
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

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(_dict.TryGetValue(key, out var val) ? val : null);

        public Task SetAsync(string key, string value)
        {
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
