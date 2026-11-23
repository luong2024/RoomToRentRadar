using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.Ports.Outbound;

public interface IHouseRulesRepository
{
    Task<RoomHouseRules?> GetByRoomIdAsync(string roomId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HouseRulesRevision>> GetRevisionsByRoomIdAsync(string roomId, CancellationToken cancellationToken = default);

    Task SaveAsync(RoomHouseRules houseRules, HouseRulesRevision newRevision, CancellationToken cancellationToken = default);
}
