using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.DeviceAdapters;

public sealed class MauiSecureSessionStore : ISessionStore
{
    private const string KeyAccessToken = "auth_access_token";
    private const string KeyRefreshToken = "auth_refresh_token";
    private const string KeyAccessExpires = "auth_access_expires";
    private const string KeyRefreshExpires = "auth_refresh_expires";
    private const string KeyUserProfile = "auth_user_profile";

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
            await SecureStorage.Default.SetAsync(KeyAccessToken, tokens.AccessToken);
            await SecureStorage.Default.SetAsync(KeyRefreshToken, tokens.RefreshToken);
            await SecureStorage.Default.SetAsync(KeyAccessExpires, tokens.AccessTokenExpiresAtUtc.ToString("o"));
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
            _logger?.LogError(ex, "Failed to persist authentication session to SecureStorage.");
            return false;
        }
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(KeyAccessToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read access token from SecureStorage.");
            return null;
        }
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
        try
        {
            var json = await SecureStorage.Default.GetAsync(KeyUserProfile);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<UserProfileDto>(json);
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
            var token = await SecureStorage.Default.GetAsync(KeyAccessToken);
            if (string.IsNullOrWhiteSpace(token))
                return false;

            var expiresStr = await SecureStorage.Default.GetAsync(KeyAccessExpires);
            if (!string.IsNullOrWhiteSpace(expiresStr) &&
                DateTime.TryParse(expiresStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiresUtc))
            {
                if (expiresUtc > DateTime.UtcNow)
                    return true;
            }

            // Check if Refresh Token is still alive for automatic refresh
            var refreshExpiresStr = await SecureStorage.Default.GetAsync(KeyRefreshExpires);
            if (!string.IsNullOrWhiteSpace(refreshExpiresStr) &&
                DateTime.TryParse(refreshExpiresStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var refreshExpiresUtc))
            {
                return refreshExpiresUtc > DateTime.UtcNow;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to validate tokens in SecureStorage.");
            return false;
        }
    }

    public Task ClearSessionAsync(CancellationToken ct = default)
    {
        try
        {
            SecureStorage.Default.Remove(KeyAccessToken);
            SecureStorage.Default.Remove(KeyRefreshToken);
            SecureStorage.Default.Remove(KeyAccessExpires);
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

