using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Rooms;

/// <summary>
/// Computes or retrieves cached campus travel times with straight-line fallback
/// (US09, UC-12, <c>operationId: getRoomTravelTimes</c>, BR-TRV-01..04).
/// </summary>
public interface IGetRoomTravelTimesUseCase
{
    Task<TravelTimesDto> ExecuteAsync(string roomId, string? campusId = null, CancellationToken ct = default);
}
