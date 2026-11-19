using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

/// <summary>Result of <see cref="IRoomPhotoRepository.DeleteUnattachedAsync"/> (BR-MED-03).</summary>
public enum PhotoDeleteOutcome
{
    NotFound,
    Attached,
    Deleted
}

/// <summary>
/// Outcome of a delete attempt. <see cref="Photo"/> is the row that was inspected so the caller can
/// remove the physical files after a successful delete; null when nothing matched the ownership scope.
/// </summary>
public sealed record PhotoDeleteAttempt(PhotoDeleteOutcome Outcome, RoomPhoto? Photo);

/// <summary>
/// Storage of listing photos (<c>docs/erd.md</c> §4.8, BR-MED-03). Every query is scoped by the
/// uploader (anti-IDOR, GUARDRAILS §1.5.6) and attachment is race-safe via a conditional UPDATE.
/// </summary>
public interface IRoomPhotoRepository
{
    Task AddAsync(RoomPhoto photo, CancellationToken ct = default);

    /// <summary>Returns the photo only when it was uploaded by <paramref name="userId"/>; otherwise null.</summary>
    Task<RoomPhoto?> GetOwnedAsync(string photoId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Deletes the photo only when it belongs to <paramref name="userId"/> and is still unattached.
    /// An attached photo yields <see cref="PhotoDeleteOutcome.Attached"/>; another owner's or a
    /// missing photo yields <see cref="PhotoDeleteOutcome.NotFound"/>.
    /// </summary>
    Task<PhotoDeleteAttempt> DeleteUnattachedAsync(string photoId, string userId, CancellationToken ct = default);

    Task<IReadOnlyList<RoomPhoto>> ListExpiredUnattachedAsync(DateTime cutoffUtc, CancellationToken ct = default);

    Task<IReadOnlyList<RoomPhoto>> ListByRoomAsync(string roomId, CancellationToken ct = default);

    Task<IReadOnlyList<RoomPhoto>> ListOwnedByIdsAsync(
        string userId,
        IReadOnlyCollection<string> photoIds,
        CancellationToken ct = default);

    /// <summary>
    /// Attaches the photos to the room and assigns <c>SortOrder</c> from the supplied order, using the
    /// conditional UPDATE of ERD §4.8 (<c>WHERE Id = @id AND UploadedByUserId = @userId AND
    /// (RoomId IS NULL OR RoomId = @roomId)</c>). Throws <c>DomainValidationException</c> when any id
    /// was not owned, expired or already attached elsewhere — the whole operation then fails.
    /// </summary>
    Task AttachAsync(
        string userId,
        string roomId,
        IReadOnlyList<string> photoIds,
        CancellationToken ct = default);

    /// <summary>Deletes the room's photos that are not in <paramref name="keepPhotoIds"/> and returns the removed rows.</summary>
    Task<IReadOnlyList<RoomPhoto>> RemoveNotInListAsync(
        string roomId,
        IReadOnlyCollection<string> keepPhotoIds,
        CancellationToken ct = default);
}
