using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Ports.Inbound;

public interface ILoginWithGoogleUseCase
{
    Task<AuthenticationResult> ExecuteAsync(
        LoginWithGoogleCommand command,
        CancellationToken cancellationToken = default);
}
