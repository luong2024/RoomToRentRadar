using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadar.Infrastructure.Favourites;

/// <summary>
/// Account-scoped favourite state shared by every screen. Reads are cached for the
/// session; writes update the cache first and roll back when the API rejects them so
/// hearts react immediately while the server stays authoritative.
/// </summary>
public sealed class FavouritesStore(IApiClient apiClient) : IFavouritesStore
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly HashSet<Guid> _listingIds = [];
    private List<FavouriteListingDto> _items = [];
    private bool _loaded;
    private int _generation;

    public event EventHandler? Changed;

    public IReadOnlyList<FavouriteListingDto> Items
    {
        get
        {
            lock (_gate)
            {
                return _items.ToArray();
            }
        }
    }

    public bool IsFavourite(Guid listingId)
    {
        lock (_gate)
        {
            return _listingIds.Contains(listingId);
        }
    }

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
        => await GetFavouritesAsync(forceRefresh: false, cancellationToken);

    public async Task<IReadOnlyList<FavouriteListingDto>> RefreshAsync(
        CancellationToken cancellationToken = default)
        => await GetFavouritesAsync(forceRefresh: true, cancellationToken);

    public async Task AddAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        int generation;
        bool added;
        lock (_gate)
        {
            generation = _generation;
            added = _listingIds.Add(listingId);
        }

        if (added)
        {
            RaiseChanged();
        }

        try
        {
            await apiClient.AddFavouriteAsync(listingId, cancellationToken);
        }
        catch
        {
            if (added)
            {
                bool rolledBack;
                lock (_gate)
                {
                    // A sign-out may have swapped accounts mid-flight; the new
                    // account's cache must not be touched by the old request.
                    rolledBack = generation == _generation && _listingIds.Remove(listingId);
                }

                if (rolledBack)
                {
                    RaiseChanged();
                }
            }

            throw;
        }
    }

    public async Task RemoveAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        FavouriteListingDto? removedItem = null;
        int generation;
        bool removed;
        lock (_gate)
        {
            generation = _generation;
            removed = _listingIds.Remove(listingId);
            if (removed)
            {
                removedItem = _items.FirstOrDefault(item => item.ListingId == listingId);
                _items.RemoveAll(item => item.ListingId == listingId);
            }
        }

        if (removed)
        {
            RaiseChanged();
        }

        try
        {
            await apiClient.RemoveFavouriteAsync(listingId, cancellationToken);
        }
        catch
        {
            if (removed)
            {
                bool rolledBack;
                lock (_gate)
                {
                    if (generation == _generation)
                    {
                        _listingIds.Add(listingId);
                        if (removedItem is not null && _items.All(item => item.ListingId != listingId))
                        {
                            _items.Add(removedItem);
                        }

                        rolledBack = true;
                    }
                    else
                    {
                        rolledBack = false;
                    }
                }

                if (rolledBack)
                {
                    RaiseChanged();
                }
            }

            throw;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _listingIds.Clear();
            _items = [];
            _loaded = false;
            _generation++;
        }

        RaiseChanged();
    }

    private async Task<IReadOnlyList<FavouriteListingDto>> GetFavouritesAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        await _loadGate.WaitAsync(cancellationToken);
        try
        {
            int generation;
            lock (_gate)
            {
                if (_loaded && !forceRefresh)
                {
                    return _items.ToArray();
                }

                generation = _generation;
            }

            var favourites = await apiClient.GetFavouritesAsync(cancellationToken);
            lock (_gate)
            {
                // Clear() ran while the request was in flight (usually because the
                // account changed), so these rows belong to the previous user.
                if (generation != _generation)
                {
                    return _items.ToArray();
                }

                _items = favourites.ToList();
                _listingIds.Clear();
                foreach (var favourite in favourites)
                {
                    _listingIds.Add(favourite.ListingId);
                }

                _loaded = true;
            }

            RaiseChanged();
            return favourites;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
