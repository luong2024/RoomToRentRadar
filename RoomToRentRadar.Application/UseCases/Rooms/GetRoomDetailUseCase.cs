using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Rooms;

public sealed class GetRoomDetailUseCase : IGetRoomDetailUseCase
{
    public const string DefaultUnavailableMessage = "Phòng trọ này hiện không còn khả dụng. Dưới đây là gợi ý các phòng tương tự gần bạn.";
    public const int DefaultSimilarRoomsLimit = 3;

    private readonly IRoomRepository _roomRepository;
    private readonly ICurrentUserContextPort _currentUserContext;

    public GetRoomDetailUseCase(IRoomRepository roomRepository, ICurrentUserContextPort currentUserContext)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    public async Task<RoomDetailDto?> ExecuteAsync(string roomId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return null;
        }

        var trimmedId = roomId.Trim();
        var room = await _roomRepository.GetByIdAsync(trimmedId, ct);
        if (room == null)
        {
            return null;
        }

        // M4: Listings pending moderation/verification must not be exposed to the public
        if (!room.IsPubliclyVisible)
        {
            return null;
        }

        // AC4: Only signed-in users can see the landlord's phone number
        var landlordPhone = _currentUserContext.IsAuthenticated ? room.LandlordPhoneNumber : null;

        // AC3: If the listing is unavailable, provide a message and suggest similar rooms nearby
        IReadOnlyList<RoomCardDto> similarRooms = Array.Empty<RoomCardDto>();
        string? unavailableMessage = null;

        if (!room.IsAvailable)
        {
            unavailableMessage = DefaultUnavailableMessage;
            var similarEntities = await _roomRepository.GetSimilarRoomsAsync(
                excludeRoomId: room.Id,
                targetPrice: room.MonthlyRent,
                targetArea: room.AreaSquareMeters,
                targetDistanceKm: room.DistanceToCampusKm,
                limit: DefaultSimilarRoomsLimit,
                ct: ct);

            if (similarEntities != null && similarEntities.Count > 0)
            {
                similarRooms = similarEntities.Select(RoomMapper.ToCardDto).ToList();
            }
        }

        // AC1 & AC2: Full details mapped through RoomMapper
        return RoomMapper.ToDetailDto(room, landlordPhone, unavailableMessage, similarRooms);
    }
}
