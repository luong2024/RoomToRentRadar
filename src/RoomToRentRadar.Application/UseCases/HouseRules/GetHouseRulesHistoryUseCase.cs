using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;

namespace RoomToRentRadar.Application.UseCases.HouseRules;

public sealed class GetHouseRulesHistoryUseCase : IGetHouseRulesHistoryUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IHouseRulesRepository _houseRulesRepository;

    public GetHouseRulesHistoryUseCase(
        IRoomRepository roomRepository,
        IHouseRulesRepository houseRulesRepository)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _houseRulesRepository = houseRulesRepository ?? throw new ArgumentNullException(nameof(houseRulesRepository));
    }

    public async Task<IReadOnlyList<HouseRulesVersionDto>> ExecuteAsync(string roomId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new RequestValidationException("roomId", "Room ID is required.");
        }

        var trimmedRoomId = roomId.Trim();
        var room = await _roomRepository.GetByIdAsync(trimmedRoomId, cancellationToken)
            ?? throw new NotFoundException($"Room '{trimmedRoomId}' was not found.");

        var revisions = await _houseRulesRepository.GetRevisionsByRoomIdAsync(room.Id, cancellationToken);
        return revisions
            .OrderByDescending(r => r.Version)
            .Select(r => r.ToDto())
            .ToList();
    }
}
