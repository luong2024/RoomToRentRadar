using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Ports.Inbound;

public interface IRefreshSessionUseCase
{
    Task<AuthenticationResult> ExecuteAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken = default);
}
