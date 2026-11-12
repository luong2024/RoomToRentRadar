using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Auth;

public interface ILoginWithGoogleUseCase
{
    Task<AuthTokensDto> ExecuteAsync(GoogleLoginRequest request, CancellationToken ct = default);
}
