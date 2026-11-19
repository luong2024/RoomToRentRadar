namespace RoomToRentRadar.Application.Ports.Inbound;

using RoomToRentRadar.Application.DTOs;

/// <summary>
/// Inbound port for querying room listings enclosed within a drawn map polygon.
/// </summary>
public interface ISearchRoomsInPolygonUseCase
{
    Task<PagedResult<SpatialRoomSearchResultDto>> ExecuteAsync(
        SpatialRoomSearchRequestDto request,
        CancellationToken cancellationToken = default);
}
