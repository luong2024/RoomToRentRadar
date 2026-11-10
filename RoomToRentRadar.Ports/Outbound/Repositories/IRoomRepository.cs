using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(string excludeRoomId, decimal targetPrice, double targetArea, int limit = 3, CancellationToken ct = default);
}
