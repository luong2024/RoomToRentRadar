using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.DeviceAdapters;

/// <summary>
/// Session store implementation adhering to ADR-005:
/// Access token is maintained in-memory only; refresh token and user profile are persisted to SecureStorage.
/// </summary>
public sealed class MauiSecureSessionStore : ISessionStore
{
    private const string KeyRefreshToken = "auth_refresh_token";
    private const string KeyRefreshExpires = "auth_refresh_expires";
    private const string KeyUserProfile = "auth_user_profile";

    // In-memory access token storage per ADR-005
    private string? _accessToken;
    private DateTimeOffset? _accessTokenExpiresAtUtc;
    private UserProfileDto? _cachedUser;

    private readonly ILogger<MauiSecureSessionStore>? _logger;

    public MauiSecureSessionStore(ILogger<MauiSecureSessionStore>? logger = null)
    {
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

            // Persist refresh token and its expiration to SecureStorage
            await SecureStorage.Default.SetAsync(KeyRefreshToken, tokens.RefreshToken);
            await SecureStorage.Default.SetAsync(KeyRefreshExpires, tokens.RefreshTokenExpiresAtUtc.ToString("o"));

            if (tokens.User != null)
            {
                var profileJson = JsonSerializer.Serialize(tokens.User);
                await SecureStorage.Default.SetAsync(KeyUserProfile, profileJson);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to persist authentication refresh token to SecureStorage.");
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
            return await SecureStorage.Default.GetAsync(KeyRefreshToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read refresh token from SecureStorage.");
            return null;
        }
    }

    public async Task<UserProfileDto?> GetCachedUserProfileAsync(CancellationToken ct = default)
    {
        if (_cachedUser != null)
            return _cachedUser;

        try
        {
            var json = await SecureStorage.Default.GetAsync(KeyUserProfile);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            _cachedUser = JsonSerializer.Deserialize<UserProfileDto>(json);
            return _cachedUser;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read cached user profile from SecureStorage.");
            return null;
        }
    }

    public async Task<bool> HasValidTokenAsync(CancellationToken ct = default)
    {
        try
        {
            // Check in-memory access token
            if (!string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAtUtc.HasValue)
            {
                if (_accessTokenExpiresAtUtc.Value > DateTimeOffset.UtcNow)
                    return true;
            }

            // Check if Refresh Token is still alive for automatic refresh
            var refreshExpiresStr = await SecureStorage.Default.GetAsync(KeyRefreshExpires);
            if (!string.IsNullOrWhiteSpace(refreshExpiresStr) &&
                DateTimeOffset.TryParse(refreshExpiresStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var refreshExpiresUtc))
            {
                return refreshExpiresUtc > DateTimeOffset.UtcNow;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to validate tokens in session store.");
            return false;
        }
    }

    public Task ClearSessionAsync(CancellationToken ct = default)
    {
        _accessToken = null;
        _accessTokenExpiresAtUtc = null;
        _cachedUser = null;

        try
        {
            SecureStorage.Default.Remove(KeyRefreshToken);
            SecureStorage.Default.Remove(KeyRefreshExpires);
            SecureStorage.Default.Remove(KeyUserProfile);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to clear session from SecureStorage.");
        }

        return Task.CompletedTask;
    }
}
