using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IRoomPhotoRepository
{
    Task AddAsync(RoomPhoto photo, CancellationToken ct = default);

    // Anti-IDOR: only returns photos uploaded by the given user.
    Task<RoomPhoto?> GetOwnedAsync(string photoId, string userId, CancellationToken ct = default);

    // Deletes only an unattached photo owned by the user; true when at least one row was removed.
    Task<bool> DeleteUnattachedAsync(string photoId, string userId, CancellationToken ct = default);

    // ORDER BY SortOrder (0 = cover).
    Task<IReadOnlyList<RoomPhoto>> ListByRoomAsync(string roomId, CancellationToken ct = default);

    // Attaches orderedIds in one transaction; returns the number of updated rows so callers can detect partial matches.
    Task<int> AttachAsync(string userId, string roomId, IReadOnlyList<string> orderedIds, CancellationToken ct = default);
}
