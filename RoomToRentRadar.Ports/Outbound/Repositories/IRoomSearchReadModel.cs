using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

/// <summary>
/// Segregated read-model query port for search and similar room suggestions per docs/system-architecture.md §6.3 and BR-LST-10.
/// </summary>
public interface IRoomSearchReadModel
{
    Task<IReadOnlyList<RoomCardDto>> GetSimilarRoomsAsync(
        string targetRoomId,
        decimal targetMonthlyRentVnd,
        double targetLatitude,
        double targetLongitude,
        RoomType roomType,
        int limit = 5,
        CancellationToken ct = default);
}
