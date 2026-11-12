using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Rooms;

public interface IGetRoomDetailUseCase
{
    Task<RoomDetailDto?> ExecuteAsync(string roomId, CancellationToken ct = default, string? campusId = null);
}
