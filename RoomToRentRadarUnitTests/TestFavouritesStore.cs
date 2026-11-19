using RoomToRentRadar.Infrastructure.Favourites;
using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadarUnitTests;

internal sealed class TestFavouritesStore : IFavouritesStore
{
    private readonly object _gate = new();
    private readonly HashSet<Guid> _listingIds = [];
    private List<FavouriteListingDto> _items;

    public TestFavouritesStore(params FavouriteListingDto[] items)
    {
        _items = [.. items];
        foreach (var item in items)
            _listingIds.Add(item.ListingId);
    }

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

    public int EnsureLoadedCount { get; private set; }

    public int RefreshCount { get; private set; }

    public int ClearCount { get; private set; }

    public List<Guid> Added { get; } = [];

    public List<Guid> Removed { get; } = [];

    public Exception? LoadException { get; set; }

    public Exception? AddException { get; set; }

    public Exception? RemoveException { get; set; }

    public IReadOnlyList<FavouriteListingDto>? NextRefresh { get; set; }

    public bool IsFavourite(Guid listingId)
    {
        lock (_gate)
        {
            return _listingIds.Contains(listingId);
        }
    }

    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        EnsureLoadedCount++;
        return LoadException is { } exception
            ? Task.FromException(exception)
            : Task.CompletedTask;
    }

    public Task<IReadOnlyList<FavouriteListingDto>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        RefreshCount++;
        if (LoadException is { } exception)
            return Task.FromException<IReadOnlyList<FavouriteListingDto>>(exception);

        if (NextRefresh is { } next)
        {
            lock (_gate)
            {
                _items = [.. next];
                _listingIds.Clear();
                foreach (var item in next)
                    _listingIds.Add(item.ListingId);
            }
        }

        return Task.FromResult(Items);
    }

    public Task AddAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        if (AddException is { } exception)
            return Task.FromException(exception);

        Added.Add(listingId);
        lock (_gate)
        {
            _listingIds.Add(listingId);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        if (RemoveException is { } exception)
            return Task.FromException(exception);

        Removed.Add(listingId);
        lock (_gate)
        {
            _listingIds.Remove(listingId);
            _items.RemoveAll(item => item.ListingId == listingId);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_gate)
        {
            _listingIds.Clear();
            _items = [];
        }

        ClearCount++;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
