using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.Ports.Outbound;

public interface IRoomRepository
{
    Task<Room> CreateAsync(Room room, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Room creation is not supported by this repository implementation.");

    Task UpdateAsync(Room room, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Room updates are not supported by this repository implementation.");

    Task<Room?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    async Task<IReadOnlyList<Room>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids is null || ids.Count == 0)
        {
            return Array.Empty<Room>();
        }

        var results = new List<Room>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawId in ids)
        {
            if (string.IsNullOrWhiteSpace(rawId))
            {
                continue;
            }

            var trimmed = rawId.Trim();
            if (!seen.Add(trimmed))
            {
                continue;
            }

            var room = await GetByIdAsync(trimmed, cancellationToken);
            if (room is not null)
            {
                results.Add(room);
            }
        }

        return results;
    }

    Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(string roomId, decimal price, double area, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Room> Items, int TotalCount)> SearchAsync(SearchRoomsQuery query, CancellationToken ct = default);

    Task<bool> AssignLandlordAsync(string roomId, Guid landlordId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
