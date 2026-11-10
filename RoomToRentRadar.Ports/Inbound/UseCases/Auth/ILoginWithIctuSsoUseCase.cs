using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Auth;

public interface ILoginWithIctuSsoUseCase
{
    Task<AuthTokensDto> ExecuteAsync(IctuSsoLoginRequest request, CancellationToken ct = default);
}

