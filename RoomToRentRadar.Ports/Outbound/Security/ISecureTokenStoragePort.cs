using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Ports.Outbound.Security;

public interface ISecureTokenStoragePort
{
    Task SaveSessionAsync(AuthTokensDto tokens, CancellationToken ct = default);
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
    Task<string?> GetRefreshTokenAsync(CancellationToken ct = default);
    Task<UserProfileDto?> GetCachedUserProfileAsync(CancellationToken ct = default);
    Task<bool> HasValidTokenAsync(CancellationToken ct = default);
    Task ClearSessionAsync(CancellationToken ct = default);
}

