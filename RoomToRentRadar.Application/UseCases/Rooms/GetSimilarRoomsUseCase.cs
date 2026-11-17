namespace RoomToRentRadar.Application.UseCases.Rooms;

using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

/// <summary>
/// Retrieves similar available rooms for an unavailable or viewable listing (US02 AC3, BR-LST-10, getSimilarRooms).
/// </summary>
public sealed class GetSimilarRoomsUseCase : IGetSimilarRoomsUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly ICampusRepository _campusRepository;
    private readonly IRoomSearchReadModel _readModel;

    public GetSimilarRoomsUseCase(
        IRoomRepository roomRepository,
        ICampusRepository campusRepository,
        IRoomSearchReadModel readModel)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _campusRepository = campusRepository ?? throw new ArgumentNullException(nameof(campusRepository));
        _readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));
    }

    public Task<IReadOnlyList<RoomCardDto>> ExecuteAsync(string roomId, string? campusId = null, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
