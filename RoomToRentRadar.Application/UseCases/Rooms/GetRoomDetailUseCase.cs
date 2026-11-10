using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Ports.Outbound.Security;

namespace RoomToRentRadar.Application.UseCases.Rooms;

public class GetRoomDetailUseCase : IGetRoomDetailUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly ICurrentUserContext _currentUserContext;

    public GetRoomDetailUseCase(IRoomRepository roomRepository, ICurrentUserContext currentUserContext)
    {
        _roomRepository = roomRepository;
        _currentUserContext = currentUserContext;
    }

    public Task<RoomDetailDto?> ExecuteAsync(string roomId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
