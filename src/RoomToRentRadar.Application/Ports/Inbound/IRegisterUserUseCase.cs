using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Ports.Inbound;

public interface IRegisterUserUseCase
{
    Task<RegisterUserResult> ExecuteAsync(
        RegisterUserCommand command,
        CancellationToken cancellationToken = default);
}
