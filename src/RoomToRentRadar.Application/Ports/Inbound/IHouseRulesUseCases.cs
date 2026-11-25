using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Ports.Inbound;

public interface IGetRoomHouseRulesUseCase
{
    Task<RoomHouseRulesDto> ExecuteAsync(string roomId, CancellationToken cancellationToken = default);
}

public interface IGetHouseRulesHistoryUseCase
{
    Task<IReadOnlyList<HouseRulesVersionDto>> ExecuteAsync(string roomId, CancellationToken cancellationToken = default);
}

public interface IUpdateRoomHouseRulesUseCase
{
    Task<RoomHouseRulesDto> ExecuteAsync(UpdateHouseRulesCommand command, CancellationToken cancellationToken = default);
}

public interface IAssignRoomLandlordUseCase
{
    Task ExecuteAsync(AssignRoomLandlordCommand command, CancellationToken cancellationToken = default);
}

public sealed record AssignRoomLandlordCommand(string RoomId, Guid LandlordId);
