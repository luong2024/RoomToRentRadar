using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadar.Infrastructure.Favourites;

public interface IFavouritesStore
{
    event EventHandler? Changed;

    IReadOnlyList<FavouriteListingDto> Items { get; }

    bool IsFavourite(Guid listingId);

    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FavouriteListingDto>> RefreshAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Guid listingId, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid listingId, CancellationToken cancellationToken = default);

    void Clear();
}
