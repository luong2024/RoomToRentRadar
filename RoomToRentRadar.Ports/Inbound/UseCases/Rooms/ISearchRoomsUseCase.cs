using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Rooms;

public interface ISearchRoomsUseCase
{
    Task<PagedResult<RoomCardDto>> ExecuteAsync(RoomSearchRequest request, CancellationToken ct = default);
}
