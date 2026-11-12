using System.Globalization;
using RoomToRentRadar.Core.Interfaces;

namespace RoomToRentRadar.Infrastructure.Auth;

public sealed class SecureTokenStore : ITokenStore
{
    private const string TokenKey = "auth_access_token";
    private const string ExpiresAtKey = "auth_access_token_expires_at";

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

    public async Task SaveAsync(
        string accessToken,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        await SecureStorage.Default.SetAsync(TokenKey, accessToken);
        await SecureStorage.Default.SetAsync(
            ExpiresAtKey,
            expiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        SecureStorage.Default.Remove(TokenKey);
        SecureStorage.Default.Remove(ExpiresAtKey);
        return Task.CompletedTask;
    }
}
