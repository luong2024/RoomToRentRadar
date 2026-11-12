using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Ports.Inbound;

public interface IGetRoomDetailsUseCase
{
    Task<RoomDetailsDto> ExecuteAsync(string roomId, bool isUserVerified, CancellationToken cancellationToken = default);
}
