using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Rooms;

/// <summary>
/// Retrieves cached or routed travel times from a room to the resolved campus (US09, UC-12, BR-TRV-01..04).
/// Phase 0 stub; full routing, caching, and fallback logic implemented in Phase 2.
/// </summary>
public sealed class GetRoomTravelTimesUseCase : IGetRoomTravelTimesUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICampusRepository _campusRepository;
    private readonly ICurrentUserContextPort _currentUserContext;
    private readonly ITravelTimeCacheRepository _travelTimeCacheRepository;
    private readonly ITravelTimePort _travelTimePort;
    private readonly IClock _clock;

    public GetRoomTravelTimesUseCase(
        IRoomRepository roomRepository,
        IUserRepository userRepository,
        ICampusRepository campusRepository,
        ICurrentUserContextPort currentUserContext,
        ITravelTimeCacheRepository travelTimeCacheRepository,
        ITravelTimePort travelTimePort,
        IClock clock)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _campusRepository = campusRepository ?? throw new ArgumentNullException(nameof(campusRepository));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _travelTimeCacheRepository = travelTimeCacheRepository ?? throw new ArgumentNullException(nameof(travelTimeCacheRepository));
        _travelTimePort = travelTimePort ?? throw new ArgumentNullException(nameof(travelTimePort));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<TravelTimesDto> ExecuteAsync(
        string roomId,
        string? campusId = null,
        CancellationToken ct = default) =>
        Task.FromResult(new TravelTimesDto("ICTU", 0, true, Array.Empty<TravelTimeItemDto>()));
}
