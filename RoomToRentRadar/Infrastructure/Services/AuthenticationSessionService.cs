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
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private readonly TaskCompletionSource<bool> _initTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private long _sessionEpoch;
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

    public bool IsInitialized => _initTcs.Task.IsCompleted;

    public AuthUser? CurrentUser => _currentUser;

    public event EventHandler<AuthUser?>? CurrentUserChanged;

    public async Task SaveSessionAsync(string accessToken, string refreshToken, AuthUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        // Immediately advance epoch on entry so any in-flight refresh started before this call is invalidated
        Interlocked.Increment(ref _sessionEpoch);

        AuthUser? committedUser;
        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            await SaveSessionCoreAsync(accessToken, refreshToken, user);
            committedUser = _currentUser;
        }
        finally
        {
            _sessionLock.Release();
        }

        CurrentUserChanged?.Invoke(this, committedUser);
    }

    private async Task SaveSessionCoreAsync(string accessToken, string refreshToken, AuthUser user)
    {
        Interlocked.Increment(ref _sessionEpoch);

        // Snapshot current in-memory state in case of rollback
        var prevAccessToken = _cachedAccessToken;
        var prevRefreshToken = _cachedRefreshToken;
        var prevUser = _currentUser;

        try
        {
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                await _secureStorage.SetAsync(AccessTokenKey, accessToken);
            }
            else
            {
                _secureStorage.Remove(AccessTokenKey);
            }

            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                await _secureStorage.SetAsync(RefreshTokenKey, refreshToken);
            }
            else
            {
                // Ensure old refresh token from previous account is wiped when switching accounts
                _secureStorage.Remove(RefreshTokenKey);
            }

            var serializedUser = JsonSerializer.Serialize(user);
            await _secureStorage.SetAsync(UserDataKey, serializedUser);

            // Successfully persisted to secure storage; update in-memory state and advance epoch at commit time
            // so any refresh that started while storage writes were in-flight is also invalidated
            _cachedAccessToken = accessToken;
            _cachedRefreshToken = refreshToken;
            _currentUser = user;
            Interlocked.Increment(ref _sessionEpoch);
            _initTcs.TrySetResult(true);
        }
        catch (Exception)
        {
            // Rollback in-memory state and advance epoch
            _cachedAccessToken = prevAccessToken;
            _cachedRefreshToken = prevRefreshToken;
            _currentUser = prevUser;
            Interlocked.Increment(ref _sessionEpoch);

            // Revert storage keys to previous snapshot or remove
            try
            {
                if (!string.IsNullOrWhiteSpace(prevAccessToken))
                {
                    await _secureStorage.SetAsync(AccessTokenKey, prevAccessToken);
                }
                else
                {
                    _secureStorage.Remove(AccessTokenKey);
                }

                if (!string.IsNullOrWhiteSpace(prevRefreshToken))
                {
                    await _secureStorage.SetAsync(RefreshTokenKey, prevRefreshToken);
                }
                else
                {
                    _secureStorage.Remove(RefreshTokenKey);
                }

                if (prevUser != null)
                {
                    await _secureStorage.SetAsync(UserDataKey, JsonSerializer.Serialize(prevUser));
                }
                else
                {
                    _secureStorage.Remove(UserDataKey);
                }
            }
            catch
            {
                // Suppress secondary cleanup exceptions to let original exception propagate
            }

            throw;
        }
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser == null)
        {
            return null;
        }

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
        if (_currentUser == null)
        {
            return null;
        }

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
        var restored = false;
        try
        {
            var accessToken = await _secureStorage.GetAsync(AccessTokenKey);
            var refreshToken = await _secureStorage.GetAsync(RefreshTokenKey);
            var userData = await _secureStorage.GetAsync(UserDataKey);

            if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(userData))
            {
                await ClearSessionAsync(cancellationToken);
                restored = false;
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
                restored = false;
                return false;
            }

            if (user == null || user.Id == Guid.Empty)
            {
                await ClearSessionAsync(cancellationToken);
                restored = false;
                return false;
            }

            // Check if access token is present and still valid
            if (!string.IsNullOrWhiteSpace(accessToken) && !IsJwtExpired(accessToken, clockSkewSeconds: 60))
            {
                _cachedAccessToken = accessToken;
                _cachedRefreshToken = refreshToken;
                _currentUser = user;
                CurrentUserChanged?.Invoke(this, _currentUser);
                restored = true;
                return true;
            }

            // Access token is missing or expired, attempt to refresh using the refresh token
            _cachedRefreshToken = refreshToken;
            _currentUser = user;
            var restoreEpoch = Interlocked.Read(ref _sessionEpoch);

            var refreshed = await RefreshTokenAsync(cancellationToken);
            if (refreshed)
            {
                restored = true;
                return true;
            }

            // If refresh failed because the token was expired or revoked, RefreshTokenAsync
            // already called ClearSessionAsync() and wiped SecureStorage.
            // If refresh failed due to a transient error (e.g. offline, timeout, server error),
            // we intentionally preserve the session in SecureStorage for later retry,
            // while ensuring in-memory state reflects that the session is not currently active
            // (unless a new session was saved concurrently while refresh was in-flight).
            if (Interlocked.Read(ref _sessionEpoch) == restoreEpoch)
            {
                _currentUser = null;
            }

            restored = false;
            return false;
        }
        catch
        {
            // Error reading secure storage or unexpected exception - do not clear storage on transient exceptions
            restored = false;
            return false;
        }
        finally
        {
            _initTcs.TrySetResult(restored);
        }
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initTcs.Task.IsCompleted)
        {
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await _initTcs.Task.WaitAsync(cts.Token);
        }
        catch (TimeoutException)
        {
            _initTcs.TrySetResult(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _initTcs.TrySetResult(false);
        }
    }

    public async Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        var initialEpoch = Interlocked.Read(ref _sessionEpoch);
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // If session epoch changed while waiting for lock (e.g. logout or account switch occurred), discard
            if (Interlocked.Read(ref _sessionEpoch) != initialEpoch)
            {
                return false;
            }

            // Double-check: If another concurrent thread just refreshed the token while we were waiting for the lock,
            // we can reuse the new valid access token instead of triggering a redundant refresh
            if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && !IsJwtExpired(_cachedAccessToken, clockSkewSeconds: 30))
            {
                return true;
            }

            var expectedUserId = _currentUser?.Id;
            var currentRefreshToken = _cachedRefreshToken ?? await _secureStorage.GetAsync(RefreshTokenKey);
            if (string.IsNullOrWhiteSpace(currentRefreshToken))
            {
                await ClearSessionAsync(cancellationToken);
                return false;
            }

            var result = await _authenticationPort.RefreshTokenAsync(currentRefreshToken, cancellationToken);

            // Fast-path check before acquiring _sessionLock: if logout or account switch occurred in-flight, discard
            if (Interlocked.Read(ref _sessionEpoch) != initialEpoch)
            {
                return false;
            }

            AuthUser? updatedUserToNotify = null;
            var sessionUpdated = false;
            var sessionCleared = false;

            await _sessionLock.WaitAsync(cancellationToken);
            try
            {
                // Commit-time verification under _sessionLock:
                // If SaveSessionAsync or ClearSessionAsync started or committed while this refresh was in-flight,
                // _sessionEpoch will have advanced and/or the active refresh token/user in memory will have changed.
                var activeRefreshToken = _cachedRefreshToken;
                if (Interlocked.Read(ref _sessionEpoch) != initialEpoch ||
                    (activeRefreshToken != null && !string.Equals(activeRefreshToken, currentRefreshToken, StringComparison.Ordinal)) ||
                    (expectedUserId.HasValue && _currentUser != null && _currentUser.Id != expectedUserId.Value))
                {
                    return false;
                }

                if (result.IsSuccess &&
                    !string.IsNullOrWhiteSpace(result.AccessToken) &&
                    !string.IsNullOrWhiteSpace(result.RefreshToken) &&
                    result.User != null)
                {
                    await SaveSessionCoreAsync(result.AccessToken, result.RefreshToken, result.User);
                    updatedUserToNotify = _currentUser;
                    sessionUpdated = true;
                }
                else if (result.FailureType is AuthFailureType.RefreshTokenExpiredOrRevoked or AuthFailureType.InvalidCredentials)
                {
                    // Clear session when the refresh token is confirmed expired, revoked, or rejected as unauthorized.
                    // For transient errors (offline network, timeout, server 5xx), keep existing session intact.
                    await ClearSessionCoreAsync();
                    sessionCleared = true;
                }
            }
            finally
            {
                _sessionLock.Release();
            }

            if (sessionUpdated)
            {
                CurrentUserChanged?.Invoke(this, updatedUserToNotify);
                return true;
            }

            if (sessionCleared)
            {
                CurrentUserChanged?.Invoke(this, null);
            }

            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Do not wipe session on unexpected exception during transport/refresh
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task ClearSessionAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _sessionEpoch);

        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            await ClearSessionCoreAsync();
        }
        finally
        {
            _sessionLock.Release();
        }

        CurrentUserChanged?.Invoke(this, null);
    }

    private async Task ClearSessionCoreAsync()
    {
        Interlocked.Increment(ref _sessionEpoch);

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
            try
            {
                await _secureStorage.SetAsync(AccessTokenKey, string.Empty);
                await _secureStorage.SetAsync(RefreshTokenKey, string.Empty);
                await _secureStorage.SetAsync(UserDataKey, string.Empty);
            }
           catch
            {
                // Storage remove/set exception suppressed
            }
        }

        Interlocked.Increment(ref _sessionEpoch);
    }

    public async Task<LogoutResult> LogoutAsync(CancellationToken cancellationToken = default)
    {
        // 1. Immediately invalidate session epoch to discard any in-flight token refresh
        Interlocked.Increment(ref _sessionEpoch);

        var serverRevoked = false;
        string? errorMessage = null;

        try
        {
            var refreshToken = _cachedRefreshToken ?? await _secureStorage.GetAsync(RefreshTokenKey);
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                try
                {
                    serverRevoked = await _authenticationPort.LogoutAsync(refreshToken, cancellationToken);
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    serverRevoked = false;
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            // 2. Unconditionally wipe local session credentials and state
            await ClearSessionAsync(cancellationToken);
            _initTcs.TrySetResult(false);
        }

        return new LogoutResult(true, serverRevoked, errorMessage);
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
