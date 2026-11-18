using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadar.Client.Adapters.Abstractions;

/// <summary>Public room browse/search operations (Rooms tag of <c>docs/api/openapi.yaml</c>); one method per operationId.</summary>
public interface IRoomGateway
{
    [ApiOperation("searchRooms")]
    Task<PagedResult<RoomCardDto>> SearchRoomsAsync(RoomSearchRequest request, CancellationToken ct = default);

    [ApiOperation("searchRoomsInArea")]
    Task<AreaSearchResultDto> SearchRoomsInAreaAsync(AreaSearchRequest request, CancellationToken ct = default);

    [ApiOperation("compareRooms")]
    Task<ComparisonDto> CompareRoomsAsync(IReadOnlyList<string> roomIds, string? campusId = null, CancellationToken ct = default);

    [ApiOperation("getRoomDetail")]
    Task<RoomDetailDto> GetRoomDetailAsync(string roomId, string? campusId = null, CancellationToken ct = default);

    [ApiOperation("getSimilarRooms")]
    Task<IReadOnlyList<RoomCardDto>> GetSimilarRoomsAsync(string roomId, string? campusId = null, CancellationToken ct = default);

    [ApiOperation("getRoomTravelTimes")]
    Task<TravelTimesDto> GetRoomTravelTimesAsync(string roomId, string? campusId = null, CancellationToken ct = default);
}