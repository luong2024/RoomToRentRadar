using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.Ports.Outbound;

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
    /// Idempotently removes a favourite room. Does not throw if the record does not exist.
    /// </summary>
    Task RemoveAsync(
        Guid userId,
        string roomId,
        CancellationToken cancellationToken = default);
}
