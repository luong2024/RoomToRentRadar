#if DEBUG
using System.Net;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadar.DeviceAdapters;

/// <summary>Debug-only decorator that simulates Explore load scenarios; never compiled into Release.</summary>
public sealed class DebugScenarioRoomGateway : IRoomGateway
{
    public enum RoomScenario
    {
        Normal,
        Empty,
        Error,
        Network,
        Slow
    }

    public static RoomScenario Scenario { get; set; } = RoomScenario.Normal;

    private readonly IRoomGateway _inner;

    public DebugScenarioRoomGateway(IRoomGateway inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task<PagedResult<RoomCardDto>> SearchRoomsAsync(RoomSearchRequest request, CancellationToken ct = default)
    {
        switch (Scenario)
        {
            case RoomScenario.Empty:
                return new PagedResult<RoomCardDto>([], 0, request.PageNumber, request.PageSize);
            case RoomScenario.Error:
                throw new ApiException("Simulated server failure", HttpStatusCode.InternalServerError, "INTERNAL_ERROR");
            case RoomScenario.Network:
                throw new ApiException("Simulated network failure", (HttpStatusCode)0, ApiErrorCodes.NetworkError);
            case RoomScenario.Slow:
                await Task.Delay(TimeSpan.FromSeconds(6), ct);
                break;
        }

        return await _inner.SearchRoomsAsync(request, ct);
    }

    public Task<AreaSearchResultDto> SearchRoomsInAreaAsync(AreaSearchRequest request, CancellationToken ct = default) =>
        _inner.SearchRoomsInAreaAsync(request, ct);

    public Task<ComparisonDto> CompareRoomsAsync(IReadOnlyList<string> roomIds, string? campusId = null, CancellationToken ct = default) =>
        _inner.CompareRoomsAsync(roomIds, campusId, ct);

    public Task<RoomDetailDto> GetRoomDetailAsync(string roomId, string? campusId = null, CancellationToken ct = default) =>
        _inner.GetRoomDetailAsync(roomId, campusId, ct);

    public Task<IReadOnlyList<RoomCardDto>> GetSimilarRoomsAsync(string roomId, string? campusId = null, CancellationToken ct = default) =>
        _inner.GetSimilarRoomsAsync(roomId, campusId, ct);

    public Task<TravelTimesDto> GetRoomTravelTimesAsync(string roomId, string? campusId = null, CancellationToken ct = default) =>
        _inner.GetRoomTravelTimesAsync(roomId, campusId, ct);
}
#endif