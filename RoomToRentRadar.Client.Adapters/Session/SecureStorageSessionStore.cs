using System.Text.Json;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Client.Adapters.Session;

/// <summary>
/// Session store implementation adhering to ADR-005:
/// Access token is maintained in-memory only; refresh token and user profile are persisted via ISecureStorageProvider.
/// </summary>
public class SecureStorageSessionStore : ISessionStore
{
    public const string KeyRefreshToken = "auth_refresh_token";
    public const string KeyRefreshExpires = "auth_refresh_expires";
    public const string KeyUserProfile = "auth_user_profile";

    // In-memory access token storage per ADR-005
    private string? _accessToken;
    private DateTimeOffset? _accessTokenExpiresAtUtc;
    private UserProfileDto? _cachedUser;

    private readonly ISecureStorageProvider _secureStorage;
    private readonly ILogger<SecureStorageSessionStore>? _logger;

    public SecureStorageSessionStore(
        ISecureStorageProvider secureStorage,
        ILogger<SecureStorageSessionStore>? logger = null)
    {
        _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
        _logger = logger;
    }

    public async Task<bool> SaveSessionAsync(AuthTokensDto tokens, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        try
        {
            // ADR-005: Access token kept in memory
            _accessToken = tokens.AccessToken;
            _accessTokenExpiresAtUtc = tokens.AccessTokenExpiresAtUtc;
            _cachedUser = tokens.User;

            // Persist refresh token and its expiration
            await _secureStorage.SetAsync(KeyRefreshToken, tokens.RefreshToken);
            await _secureStorage.SetAsync(KeyRefreshExpires, tokens.RefreshTokenExpiresAtUtc.ToString("o"));

            if (tokens.User != null)
            {
                var profileJson = JsonSerializer.Serialize(tokens.User);
                await _secureStorage.SetAsync(KeyUserProfile, profileJson);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to persist authentication refresh token to secure storage.");
            return false;
        }
    }

    public Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        // ADR-005: Return in-memory access token
        return Task.FromResult(_accessToken);
    }

    public async Task<string?> GetRefreshTokenAsync(CancellationToken ct = default)
    {
        try
        {
            return await _secureStorage.GetAsync(KeyRefreshToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read refresh token from secure storage.");
            return null;
        }
    }

    public async Task<UserProfileDto?> GetCachedUserProfileAsync(CancellationToken ct = default)
    {
        if (_cachedUser != null)
            return _cachedUser;

        try
        {
            var json = await _secureStorage.GetAsync(KeyUserProfile);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            _cachedUser = JsonSerializer.Deserialize<UserProfileDto>(json);
            return _cachedUser;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read cached user profile from secure storage.");
            return null;
        }
    }

    public async Task<bool> HasValidTokenAsync(CancellationToken ct = default)
    {
        // Check if access token is still valid in-memory
        if (!string.IsNullOrEmpty(_accessToken) && _accessTokenExpiresAtUtc.HasValue)
        {
            if (_accessTokenExpiresAtUtc.Value > DateTimeOffset.UtcNow)
                return true;
        }

        // Fallback: check if refresh token exists and hasn't expired
        var refreshToken = await GetRefreshTokenAsync(ct);
        if (string.IsNullOrEmpty(refreshToken))
            return false;

        try
        {
            var expiresStr = await _secureStorage.GetAsync(KeyRefreshExpires);
            if (!string.IsNullOrEmpty(expiresStr) && DateTimeOffset.TryParse(expiresStr, out var expires))
            {
                return expires > DateTimeOffset.UtcNow;
            }
        }
        catch
        {
            // If expiry parsing fails, assume valid if token exists
        }

        return true;
    }

    public async Task ClearSessionAsync(CancellationToken ct = default)
    {
        _accessToken = null;
        _accessTokenExpiresAtUtc = null;
        _cachedUser = null;

        try
        {
            _secureStorage.Remove(KeyRefreshToken);
            _secureStorage.Remove(KeyRefreshExpires);
            _secureStorage.Remove(KeyUserProfile);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to clear secure storage credentials.");
        }

        await Task.CompletedTask;
    }
}

