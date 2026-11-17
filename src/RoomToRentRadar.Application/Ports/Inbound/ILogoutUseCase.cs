using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Ports.Inbound;

public interface ILogoutUseCase
{
    Task ExecuteAsync(
        LogoutCommand command,
        CancellationToken cancellationToken = default);
}
