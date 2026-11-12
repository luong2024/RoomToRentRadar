using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Rooms;

public sealed class GetRoomDetailUseCase : IGetRoomDetailUseCase
{
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

        // BR-LST-04a: Listings pending review, rejected, or suspended return 404 to non-owner/public callers
        if (!room.IsPubliclyVisible)
        {
            return null;
        }

        // BR-AUTH-07 / US02 AC4: Only signed-in users can see the landlord's phone number
        var landlordPhone = _currentUserContext.IsAuthenticated ? room.LandlordPhoneNumber : null;

        // AC1 & AC2: Full details mapped through RoomMapper
        return RoomMapper.ToDetailDto(room, landlordPhone);
    }
}
