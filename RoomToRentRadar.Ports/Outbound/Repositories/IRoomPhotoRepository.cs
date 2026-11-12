using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public enum RoomPhotoDeleteOutcome
{
    Deleted,
    NotFound,
    Attached
}

public interface IRoomPhotoRepository
{
    Task AddAsync(RoomPhoto photo, CancellationToken ct = default);

    // Anti-IDOR: only returns photos uploaded by the given user.
    Task<RoomPhoto?> GetOwnedAsync(string photoId, string userId, CancellationToken ct = default);

    // Atomically deletes an unattached photo owned by the user and reports why nothing was deleted,
    // so a concurrent attach surfaces as Attached instead of a misleading NotFound (BR-MED-03).
    Task<RoomPhotoDeleteOutcome> DeleteUnattachedAsync(string photoId, string userId, CancellationToken ct = default);

    // Expired unattached photos for the cleanup job (BR-MED-03), oldest first.
    Task<IReadOnlyList<RoomPhoto>> ListExpiredUnattachedAsync(
        DateTime cutoffUtc,
        int maxCount,
        CancellationToken ct = default);

    // ORDER BY SortOrder (0 = cover).
    Task<IReadOnlyList<RoomPhoto>> ListByRoomAsync(string roomId, CancellationToken ct = default);

    // Runs in the caller's transaction when one is active. Unattached photos older than 24 h never
    // attach (BR-MED-03). Returns the number of updated rows; the caller must fail and roll back the
    // whole operation when it is less than orderedIds.Count.
    // Without an active caller transaction updates are not atomic; all-or-nothing needs IUnitOfWork.ExecuteAsync.
    Task<int> AttachAsync(string userId, string roomId, IReadOnlyList<string> orderedIds, CancellationToken ct = default);
}
