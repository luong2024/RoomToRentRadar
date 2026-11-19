using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadar.Services;

public sealed class FavouritesService(IFavouritesApiClient apiClient) : IFavouritesService
{
    private readonly List<FavouriteRoomDto> _items = [];
    private readonly HashSet<Guid> _favouriteIds = [];

    public IReadOnlyList<FavouriteRoomDto> Items => _items;

    public bool IsFavourite(Guid listingId) => _favouriteIds.Contains(listingId);

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var result = await apiClient.GetFavouritesAsync(cancellationToken);

        _items.Clear();
        _favouriteIds.Clear();

        if (result.Outcome == FavouritesFetchOutcome.Unauthorized)
        {
            return false;
        }

        _items.AddRange(result.Items);
        _favouriteIds.UnionWith(result.Items.Select(item => item.ListingId));
        return true;
    }

    public async Task AddAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        await apiClient.AddFavouriteAsync(listingId, cancellationToken);
        _favouriteIds.Add(listingId);
    }

    public async Task<FavouriteRoomDto?> RemoveAsync(
        Guid listingId,
        CancellationToken cancellationToken = default)
    {
        var index = _items.FindIndex(item => item.ListingId == listingId);
        var removed = index >= 0 ? _items[index] : null;

        await apiClient.RemoveFavouriteAsync(listingId, cancellationToken);

        if (index >= 0)
        {
            _items.RemoveAt(index);
        }

        _favouriteIds.Remove(listingId);
        return removed;
    }

    public async Task RestoreAsync(
        FavouriteRoomDto item,
        CancellationToken cancellationToken = default)
    {
        await apiClient.AddFavouriteAsync(item.ListingId, cancellationToken);

        _favouriteIds.Add(item.ListingId);
        if (!_items.Exists(existing => existing.ListingId == item.ListingId))
        {
            _items.Insert(0, item);
        }
    }
}
