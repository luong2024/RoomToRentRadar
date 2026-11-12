using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Rooms;

/// <summary>
/// Driving inbound port for querying available similar rooms near an unavailable listing per docs/business-analysis.md BR-LST-10.
/// </summary>
public interface IGetSimilarRoomsUseCase
{
    Task<IReadOnlyList<RoomCardDto>> ExecuteAsync(string roomId, string? campusId = null, CancellationToken ct = default);
}
