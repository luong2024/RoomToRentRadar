using System.Text;
using System.Text.Json;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;

namespace RoomToRentRadar.Infrastructure.Services;

/// <summary>
/// Implements IAuthenticationSessionService to securely persist authentication tokens and user state,
/// restore sessions on startup, and coordinate token refresh with token rotation.
/// </summary>
public class AuthenticationSessionService : IAuthenticationSessionService
{
    public const string AccessTokenKey = "auth_access_token";
    public const string RefreshTokenKey = "auth_refresh_token";
    public const string UserDataKey = "auth_user_data";

    private readonly ISecureStorageService _secureStorage;
    private readonly IAuthenticationPort _authenticationPort;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _cachedAccessToken;
    private string? _cachedRefreshToken;
    private AuthUser? _currentUser;

    public AuthenticationSessionService(
        ISecureStorageService secureStorage,
        IAuthenticationPort authenticationPort)
    {
        _secureStorage = secureStorage;
        _authenticationPort = authenticationPort;
    }

    public bool IsAuthenticated => _currentUser != null && !string.IsNullOrWhiteSpace(_cachedAccessToken);

    public AuthUser? CurrentUser => _currentUser;

    public event EventHandler<AuthUser?>? CurrentUserChanged;

    public async Task SaveSessionAsync(string accessToken, string refreshToken, AuthUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        _cachedAccessToken = accessToken;
        _cachedRefreshToken = refreshToken;
        _currentUser = user;

        try
        {
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                await _secureStorage.SetAsync(AccessTokenKey, accessToken);
            }

            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                await _secureStorage.SetAsync(RefreshTokenKey, refreshToken);
            }

            var serializedUser = JsonSerializer.Serialize(user);
            await _secureStorage.SetAsync(UserDataKey, serializedUser);
        }
        catch (Exception)
        {
            // Storage error fallback, in-memory state still holds
        }

        CurrentUserChanged?.Invoke(this, _currentUser);
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_cachedAccessToken))
        {
            return _cachedAccessToken;
        }

        try
        {
            _cachedAccessToken = await _secureStorage.GetAsync(AccessTokenKey);
            return _cachedAccessToken;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_cachedRefreshToken))
        {
            return _cachedRefreshToken;
        }

        try
        {
            _cachedRefreshToken = await _secureStorage.GetAsync(RefreshTokenKey);
            return _cachedRefreshToken;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var accessToken = await _secureStorage.GetAsync(AccessTokenKey);
            var refreshToken = await _secureStorage.GetAsync(RefreshTokenKey);
            var userData = await _secureStorage.GetAsync(UserDataKey);

            if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(userData))
            {
                await ClearSessionAsync(cancellationToken);
                return false;
            }

            AuthUser? user;
            try
            {
                user = JsonSerializer.Deserialize<AuthUser>(userData);
            }
            catch
            {
                // Corrupted user JSON
                await ClearSessionAsync(cancellationToken);
                return false;
            }

            if (user == null || user.Id == Guid.Empty)
            {
                await ClearSessionAsync(cancellationToken);
                return false;
            }

            // Check if access token is present and still valid
            if (!string.IsNullOrWhiteSpace(accessToken) && !IsJwtExpired(accessToken, clockSkewSeconds: 60))
            {
                _cachedAccessToken = accessToken;
                _cachedRefreshToken = refreshToken;
                _currentUser = user;
                CurrentUserChanged?.Invoke(this, _currentUser);
                return true;
            }

            // Access token is missing or expired, attempt to refresh using the refresh token
            _cachedRefreshToken = refreshToken;
            _currentUser = user;

            var refreshed = await RefreshTokenAsync(cancellationToken);
            if (refreshed)
            {
                return true;
            }

            await ClearSessionAsync(cancellationToken);
            return false;
        }
        catch
        {
            // Error reading secure storage or unexpected exception
            await ClearSessionAsync(cancellationToken);
            return false;
        }
    }

    public async Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var currentRefreshToken = _cachedRefreshToken ?? await _secureStorage.GetAsync(RefreshTokenKey);
            if (string.IsNullOrWhiteSpace(currentRefreshToken))
            {
                await ClearSessionAsync(cancellationToken);
                return false;
            }

            var result = await _authenticationPort.RefreshTokenAsync(currentRefreshToken, cancellationToken);
            if (result.IsSuccess &&
                !string.IsNullOrWhiteSpace(result.AccessToken) &&
                !string.IsNullOrWhiteSpace(result.RefreshToken) &&
                result.User != null)
            {
                await SaveSessionAsync(result.AccessToken, result.RefreshToken, result.User, cancellationToken);
                return true;
            }

            // Refresh failed (e.g. token expired, revoked, invalid)
            await ClearSessionAsync(cancellationToken);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await ClearSessionAsync(cancellationToken);
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task ClearSessionAsync(CancellationToken cancellationToken = default)
    {
        _cachedAccessToken = null;
        _cachedRefreshToken = null;
        _currentUser = null;

        try
        {
            _secureStorage.Remove(AccessTokenKey);
            _secureStorage.Remove(RefreshTokenKey);
            _secureStorage.Remove(UserDataKey);
        }
        catch
        {
            // Storage remove exception suppressed
        }

        CurrentUserChanged?.Invoke(this, null);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Checks whether a JWT is expired based on its 'exp' claim.
    /// </summary>
    public static bool IsJwtExpired(string jwt, int clockSkewSeconds = 60)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return true;
        }

        var parts = jwt.Split('.');
        if (parts.Length != 3)
        {
            return true;
        }

        try
        {
            var payloadJson = DecodeBase64Url(parts[1]);
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("exp", out var expProp) &&
                expProp.TryGetInt64(out var expSeconds))
            {
                var expiration = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
                return DateTimeOffset.UtcNow.AddSeconds(clockSkewSeconds) >= expiration;
            }

            return true;
        }
        catch
        {
            return true;
        }
    }

    private static string DecodeBase64Url(string base64Url)
    {
        var padded = base64Url.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        var bytes = Convert.FromBase64String(padded);
        return Encoding.UTF8.GetString(bytes);
    }
}
