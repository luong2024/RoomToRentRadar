using System.Globalization;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;

namespace RoomToRentRadar.Services.Auth;

public sealed class SecureTokenStore : ITokenStore
{
    private const string TokenKey = "auth_access_token";
    private const string ExpiresAtKey = "auth_access_token_expires_at";
    private const string RoleKey = "auth_access_token_role";

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var token = await SecureStorage.Default.GetAsync(TokenKey);
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var expiresAtRaw = await SecureStorage.Default.GetAsync(ExpiresAtKey);
        if (!DateTime.TryParse(
                expiresAtRaw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var expiresAt)
            || expiresAt <= DateTime.UtcNow)
        {
            await ClearAsync(cancellationToken);
            return null;
        }

        return token;
    }

    public async Task<AppUserRole?> GetRoleAsync(CancellationToken cancellationToken = default)
        => AppUserRoleWire.TryParse(await SecureStorage.Default.GetAsync(RoleKey));

    public async Task SaveAsync(
        string accessToken,
        DateTime expiresAt,
        AppUserRole role,
        CancellationToken cancellationToken = default)
    {
        await SecureStorage.Default.SetAsync(TokenKey, accessToken);
        await SecureStorage.Default.SetAsync(
            ExpiresAtKey,
            expiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        await SecureStorage.Default.SetAsync(RoleKey, AppUserRoleWire.ToWireValue(role));
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        SecureStorage.Default.Remove(TokenKey);
        SecureStorage.Default.Remove(ExpiresAtKey);
        SecureStorage.Default.Remove(RoleKey);
        return Task.CompletedTask;
    }
}
