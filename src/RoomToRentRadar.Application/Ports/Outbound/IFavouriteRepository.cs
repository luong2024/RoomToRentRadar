using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.Ports.Outbound;

public enum FavouriteAddStatus
{
    Created = 0,
    AlreadyExists = 1,
    QuotaExceeded = 2
}

/// <summary>
/// Outbound port for persisting and querying user favourite rooms (US06 / T06.1).
/// </summary>
public interface IFavouriteRepository
{
    Task<FavouriteRoom?> GetByUserAndRoomIdAsync(
        Guid userId,
        string roomId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FavouriteRoom>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<int> CountByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotently persists a favourite room. If a concurrent insert for the same (UserId, RoomId)
    /// already succeeded, returns the existing FavouriteRoom with WasCreated = false.
    /// </summary>
    Task<(FavouriteRoom Favourite, bool WasCreated)> AddIfNotExistsAsync(
        FavouriteRoom favourite,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically checks the per-user favourite quota and persists the favourite room if within quota.
    /// Guarantees that concurrent writes for different rooms under the same user cannot exceed maxQuota.
    /// </summary>
    async Task<(FavouriteRoom? Favourite, FavouriteAddStatus Status)> TryAddWithQuotaAsync(
        FavouriteRoom favourite,
        int maxQuota,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(favourite);

        var existing = await GetByUserAndRoomIdAsync(favourite.UserId, favourite.RoomId, cancellationToken);
        if (existing is not null)
        {
            return (existing, FavouriteAddStatus.AlreadyExists);
        }

        var count = await CountByUserIdAsync(favourite.UserId, cancellationToken);
        if (count >= maxQuota)
        {
            return (null, FavouriteAddStatus.QuotaExceeded);
        }

        var (saved, wasCreated) = await AddIfNotExistsAsync(favourite, cancellationToken);
        return (saved, wasCreated ? FavouriteAddStatus.Created : FavouriteAddStatus.AlreadyExists);
    }

    /// <summary>
    /// Idempotently removes a favourite room. Does not throw if the record does not exist.
    /// </summary>
    Task RemoveAsync(
        Guid userId,
        string roomId,
        CancellationToken cancellationToken = default);
}
