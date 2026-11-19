namespace RoomToRentRadar.Ports.Inbound.UseCases.Rooms;

using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// Retrieves the full details of a room listing (US02, UC-03, getRoomDetail).
/// </summary>
public interface IGetRoomDetailUseCase
{
    Task<RoomDetailDto> ExecuteAsync(string roomId, string? campusId, CancellationToken ct = default);
}
