using RoomToRentRadar.Domain.Aggregates;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

/// <summary>
/// Driven port for Room aggregate persistence and retrieval per docs/system-architecture.md §6.3 and PR #16 foundation.
/// </summary>
public interface IRoomRepository
{
    Task<IReadOnlyList<Room>> GetAllAsync(CancellationToken ct = default);
    Task<Room?> GetByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(Room room, CancellationToken ct = default);
}
