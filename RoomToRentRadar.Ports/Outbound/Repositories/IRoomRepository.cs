using RoomToRentRadar.Domain.Aggregates;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

/// <summary>
/// Driven port for Room persistence and queries.
/// </summary>
public interface IRoomRepository
{
    /// <summary>
    /// Retrieves a room by its unique identifier. Returns null if not found.
    /// </summary>
    Task<Room?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves available similar rooms excluding the specified room, ordered by distance to campus, price difference, and area difference.
    /// Contract semantics:
    /// - Only rooms with Status == Available are returned.
    /// - The room with ID matching excludeRoomId is excluded.
    /// - Results are limited to the specified limit (at least 1).
    /// </summary>
    Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(
        string excludeRoomId,
        decimal targetPrice,
        double targetArea,
        double targetDistanceKm = 0.0,
        int limit = 3,
        CancellationToken ct = default);
}
