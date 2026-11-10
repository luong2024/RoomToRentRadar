using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Ports.Inbound;

public interface ILoginWithEmailUseCase
{
    Task<AuthenticationResult> ExecuteAsync(
        LoginWithEmailCommand command,
        CancellationToken cancellationToken = default);
}
