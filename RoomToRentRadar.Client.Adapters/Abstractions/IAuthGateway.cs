using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Client.Adapters.Abstractions;

public interface IAuthGateway
{
    Task<AuthTokensDto> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthTokensDto> RegisterAsync(RegisterUserRequest request, CancellationToken ct = default);
    Task<AuthTokensDto> LoginWithIctuSsoAsync(IctuSsoLoginRequest request, CancellationToken ct = default);
    Task<AuthTokensDto> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task RevokeTokenAsync(RevokeTokenRequest request, string? bearerToken = null, CancellationToken ct = default);
    Task<UserProfileDto> GetCurrentUserProfileAsync(string? bearerToken = null, CancellationToken ct = default);
}

