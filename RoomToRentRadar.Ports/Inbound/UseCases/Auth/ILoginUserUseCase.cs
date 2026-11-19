using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Auth;

public interface ILoginUserUseCase
{
    Task<AuthTokensDto> ExecuteAsync(LoginRequest request, CancellationToken ct = default);
}

