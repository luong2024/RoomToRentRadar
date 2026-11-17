namespace RoomToRentRadar.Application.UseCases.Rooms;

using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

/// <summary>
/// Retrieves the full details of a room listing (US02, UC-03, getRoomDetail).
/// </summary>
public sealed class GetRoomDetailUseCase : IGetRoomDetailUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICampusRepository _campusRepository;
    private readonly ICurrentUserContextPort _currentUserContext;

    public GetRoomDetailUseCase(
        IRoomRepository roomRepository,
        IUserRepository userRepository,
        ICampusRepository campusRepository,
        ICurrentUserContextPort currentUserContext)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _campusRepository = campusRepository ?? throw new ArgumentNullException(nameof(campusRepository));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    public Task<RoomDetailDto> ExecuteAsync(string roomId, string? campusId = null, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
