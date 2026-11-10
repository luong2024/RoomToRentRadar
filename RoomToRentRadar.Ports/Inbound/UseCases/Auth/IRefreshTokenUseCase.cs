using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Auth;

public interface IRefreshTokenUseCase
{
    Task<AuthTokensDto> ExecuteAsync(RefreshTokenRequest request, CancellationToken ct = default);
}

