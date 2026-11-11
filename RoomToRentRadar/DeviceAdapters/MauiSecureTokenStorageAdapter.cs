using System.Text.Json;
using Microsoft.Maui.Storage;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Outbound.Security;

namespace RoomToRentRadar.DeviceAdapters;

public sealed class MauiSecureTokenStorageAdapter : ISecureTokenStoragePort
{
    private const string KeyAccessToken = "auth_access_token";
    private const string KeyRefreshToken = "auth_refresh_token";
    private const string KeyAccessExpires = "auth_access_expires";
    private const string KeyRefreshExpires = "auth_refresh_expires";
    private const string KeyUserProfile = "auth_user_profile";

    public async Task SaveSessionAsync(AuthTokensDto tokens, CancellationToken ct = default)
    {
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
        }
        catch (Exception)
        {
            // Graceful fallback for environments where SecureStorage hardware key is restricted
        }
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(KeyAccessToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<string?> GetRefreshTokenAsync(CancellationToken ct = default)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(KeyRefreshToken);
        }
        catch (Exception)
        {
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
        catch (Exception)
        {
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
            if (!string.IsNullOrWhiteSpace(expiresStr) && DateTime.TryParse(expiresStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiresUtc))
            {
                if (expiresUtc > DateTime.UtcNow)
                    return true;
            }

            // Check if Refresh Token is still alive for automatic refresh
            var refreshExpiresStr = await SecureStorage.Default.GetAsync(KeyRefreshExpires);
            if (!string.IsNullOrWhiteSpace(refreshExpiresStr) && DateTime.TryParse(refreshExpiresStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var refreshExpiresUtc))
            {
                return refreshExpiresUtc > DateTime.UtcNow;
            }

            return false;
        }
        catch (Exception)
        {
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
        catch (Exception)
        {
            // Ignore removal errors
        }

        return Task.CompletedTask;
    }
}

