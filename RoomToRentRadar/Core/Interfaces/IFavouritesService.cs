using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadar.Core.Interfaces;

public interface IFavouritesService
{
    /// <summary>
    /// Favourites from the last successful refresh, most recently saved first.
    /// </summary>
    IReadOnlyList<FavouriteRoomDto> Items { get; }

    bool IsFavourite(Guid listingId);

    /// <summary>
    /// Reloads the favourites from the account. Returns <c>false</c> when the
    /// user is not signed in.
    /// </summary>
    Task<bool> RefreshAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the favourite and returns the saved row so it can be offered
    /// back through undo. Returns <c>null</c> when the cache had no copy.
    /// </summary>
    Task<FavouriteRoomDto?> RemoveAsync(Guid listingId, CancellationToken cancellationToken = default);

    Task RestoreAsync(FavouriteRoomDto item, CancellationToken cancellationToken = default);
}
