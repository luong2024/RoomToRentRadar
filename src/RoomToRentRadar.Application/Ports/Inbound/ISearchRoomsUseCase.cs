using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Ports.Inbound;

public interface ISearchRoomsUseCase
{
    Task<SearchRoomsResult> ExecuteAsync(SearchRoomsQuery query, CancellationToken cancellationToken = default);
}
