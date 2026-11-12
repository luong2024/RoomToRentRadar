using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Client.Adapters.Abstractions;

public interface ISessionStore
{
    Task<bool> SaveSessionAsync(AuthTokensDto tokens, CancellationToken ct = default);
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
    Task<string?> GetRefreshTokenAsync(CancellationToken ct = default);
    Task<UserProfileDto?> GetCachedUserProfileAsync(CancellationToken ct = default);
    Task<bool> HasValidTokenAsync(CancellationToken ct = default);
    Task ClearSessionAsync(CancellationToken ct = default);
}

