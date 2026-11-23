using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;

namespace RoomToRentRadar.Application.UseCases.HouseRules;

public sealed class GetRoomHouseRulesUseCase : IGetRoomHouseRulesUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IHouseRulesRepository _houseRulesRepository;

    public GetRoomHouseRulesUseCase(
        IRoomRepository roomRepository,
        IHouseRulesRepository houseRulesRepository)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _houseRulesRepository = houseRulesRepository ?? throw new ArgumentNullException(nameof(houseRulesRepository));
    }

    public async Task<RoomHouseRulesDto> ExecuteAsync(string roomId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new RequestValidationException("roomId", "Room ID is required.");
        }

        var trimmedRoomId = roomId.Trim();
        var room = await _roomRepository.GetByIdAsync(trimmedRoomId, cancellationToken)
            ?? throw new NotFoundException($"Room '{trimmedRoomId}' was not found.");

        var houseRules = await _houseRulesRepository.GetByRoomIdAsync(room.Id, cancellationToken);
        return houseRules is not null
            ? houseRules.ToDto()
            : HouseRulesMappingExtensions.CreateLegacyFallbackDto(room);
    }
}
