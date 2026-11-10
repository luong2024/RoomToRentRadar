using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Auth;

public interface IRegisterUserUseCase
{
    Task<AuthTokensDto> ExecuteAsync(RegisterUserRequest request, CancellationToken ct = default);
}

