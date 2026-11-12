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

    // Runs in the caller's transaction when one is active. Unattached photos older than 24 h never
    // attach (BR-MED-03). Returns the number of updated rows; the caller must fail and roll back the
    // whole operation when it is less than orderedIds.Count.
    // Without an active caller transaction updates are not atomic; all-or-nothing needs IUnitOfWork.ExecuteAsync.
    Task<int> AttachAsync(string userId, string roomId, IReadOnlyList<string> orderedIds, CancellationToken ct = default);
}
