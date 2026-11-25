using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

/// <summary>Cached travel-time row stored per (RoomId, CampusId, Mode) (<c>docs/erd.md</c> §4.15).</summary>
public sealed record TravelTimeCacheEntry(
    string RoomId,
    string CampusId,
    TravelMode Mode,
    int DistanceMeters,
    int DurationSeconds,
    string Source,
    DateTime ComputedAtUtc);

/// <summary>Persistence port for 30-day travel time cache (<c>TravelTimeEstimates</c>, BR-TRV-03).</summary>
public interface ITravelTimeCacheRepository
{
    Task<IReadOnlyList<TravelTimeCacheEntry>> GetByRoomAndCampusAsync(
        string roomId,
        string campusId,
        CancellationToken ct = default);

    Task UpsertRangeAsync(
        IReadOnlyList<TravelTimeCacheEntry> entries,
        CancellationToken ct = default);

    Task InvalidateByRoomIdAsync(
        string roomId,
        CancellationToken ct = default);

    Task<int> PruneExpiredAsync(
        DateTime expiresBeforeUtc,
        CancellationToken ct = default);
}
