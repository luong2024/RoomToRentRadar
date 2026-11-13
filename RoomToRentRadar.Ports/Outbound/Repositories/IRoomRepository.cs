namespace RoomToRentRadar.Ports.Outbound.Repositories;

using RoomToRentRadar.Domain.Aggregates;

/// <summary>Lifecycle storage of the Room aggregate root (write side and by-id reads).</summary>
public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(Room room, CancellationToken ct = default);
    Task UpdateAsync(Room room, CancellationToken ct = default);
}
