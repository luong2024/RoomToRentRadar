using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services.Auth;

public interface ITokenStore
{
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    Task<AppUserRole?> GetRoleAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        string accessToken,
        DateTime expiresAt,
        AppUserRole role,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
