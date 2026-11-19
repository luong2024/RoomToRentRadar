using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Infrastructure.Favourites;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Favourites;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Notifications;
using RoomToRentRadar.Shared.SavedSearches;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class FavouritesStoreTests
{
    [Test]
    public async Task EnsureLoadedAsync_FirstCall_LoadsFavouritesAndIndexesIds()
    {
        var favourite = CreateFavourite("Sunny room");
        var apiClient = new FakeApiClient { Favourites = [favourite] };
        var store = new FavouritesStore(apiClient);
        var changedCount = 0;
        store.Changed += (_, _) => changedCount++;

        await store.EnsureLoadedAsync();

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.GetCount, Is.EqualTo(1));
            Assert.That(store.Items, Has.Count.EqualTo(1));
            Assert.That(store.Items[0].ListingId, Is.EqualTo(favourite.ListingId));
            Assert.That(store.IsFavourite(favourite.ListingId), Is.True);
            Assert.That(changedCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task EnsureLoadedAsync_WhenAlreadyLoaded_DoesNotCallApiAgain()
    {
        var apiClient = new FakeApiClient { Favourites = [CreateFavourite("Sunny room")] };
        var store = new FavouritesStore(apiClient);

        await store.EnsureLoadedAsync();
        await store.EnsureLoadedAsync();

        Assert.That(apiClient.GetCount, Is.EqualTo(1));
    }

    [Test]
    public async Task RefreshAsync_AlwaysReloadsAndReplacesCachedItems()
    {
        var first = CreateFavourite("First room");
        var second = CreateFavourite("Second room");
        var apiClient = new FakeApiClient { Favourites = [first] };
        var store = new FavouritesStore(apiClient);
        await store.EnsureLoadedAsync();

        apiClient.Favourites = [second];
        var refreshed = await store.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.GetCount, Is.EqualTo(2));
            Assert.That(refreshed.Select(item => item.ListingId), Is.EqualTo(new[] { second.ListingId }));
            Assert.That(store.Items.Select(item => item.ListingId), Is.EqualTo(new[] { second.ListingId }));
            Assert.That(store.IsFavourite(first.ListingId), Is.False);
            Assert.That(store.IsFavourite(second.ListingId), Is.True);
        });
    }

    [Test]
    public async Task AddAsync_FillsHeartBeforeRequestCompletesAndSendsPut()
    {
        var apiClient = new FakeApiClient();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        apiClient.AddGate = pending.Task;
        var store = new FavouritesStore(apiClient);
        var listingId = Guid.NewGuid();

        var addTask = store.AddAsync(listingId);

        Assert.That(store.IsFavourite(listingId), Is.True);

        pending.SetResult();
        await addTask;

        Assert.That(apiClient.Added, Is.EqualTo(new[] { listingId }));
    }

    [Test]
    public async Task AddAsync_WhenApiFails_RollsBackHeartAndThrows()
    {
        var exception = new HttpRequestException("offline");
        var apiClient = new FakeApiClient { AddException = exception };
        var store = new FavouritesStore(apiClient);
        var listingId = Guid.NewGuid();

        var thrown = Assert.ThrowsAsync<HttpRequestException>(() => store.AddAsync(listingId));

        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.SameAs(exception));
            Assert.That(store.IsFavourite(listingId), Is.False);
        });
    }

    [Test]
    public async Task RemoveAsync_RemovesItemImmediatelyAndSendsDelete()
    {
        var favourite = CreateFavourite("Sunny room");
        var apiClient = new FakeApiClient { Favourites = [favourite] };
        var store = new FavouritesStore(apiClient);
        await store.EnsureLoadedAsync();

        await store.RemoveAsync(favourite.ListingId);

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.Removed, Is.EqualTo(new[] { favourite.ListingId }));
            Assert.That(store.IsFavourite(favourite.ListingId), Is.False);
            Assert.That(store.Items, Is.Empty);
        });
    }

    [Test]
    public async Task RemoveAsync_WhenApiFails_RestoresItemAndThrows()
    {
        var favourite = CreateFavourite("Sunny room");
        var exception = new HttpRequestException("offline");
        var apiClient = new FakeApiClient { Favourites = [favourite], RemoveException = exception };
        var store = new FavouritesStore(apiClient);
        await store.EnsureLoadedAsync();

        var thrown = Assert.ThrowsAsync<HttpRequestException>(() => store.RemoveAsync(favourite.ListingId));

        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.SameAs(exception));
            Assert.That(store.IsFavourite(favourite.ListingId), Is.True);
            Assert.That(store.Items.Select(item => item.ListingId), Is.EqualTo(new[] { favourite.ListingId }));
        });
    }

    [Test]
    public async Task EnsureLoadedAsync_WhenCacheIsClearedMidFlight_DiscardsPreviousAccountItems()
    {
        var previousAccountFavourite = CreateFavourite("Previous account room");
        var apiClient = new FakeApiClient { Favourites = [previousAccountFavourite] };
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        apiClient.GetGate = gate.Task;
        var store = new FavouritesStore(apiClient);

        var loadTask = store.EnsureLoadedAsync();
        store.Clear();
        gate.SetResult();
        await loadTask;

        Assert.Multiple(() =>
        {
            Assert.That(store.Items, Is.Empty);
            Assert.That(store.IsFavourite(previousAccountFavourite.ListingId), Is.False);
        });

        apiClient.Favourites = [CreateFavourite("New account room")];
        await store.EnsureLoadedAsync();

        Assert.That(apiClient.GetCount, Is.EqualTo(2));
    }

    [Test]
    public async Task RemoveAsync_WhenCacheIsClearedMidFlight_DoesNotRestorePreviousAccountItem()
    {
        var favourite = CreateFavourite("Previous account room");
        var exception = new HttpRequestException("offline");
        var apiClient = new FakeApiClient { Favourites = [favourite], RemoveException = exception };
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        apiClient.RemoveGate = gate.Task;
        var store = new FavouritesStore(apiClient);
        await store.EnsureLoadedAsync();

        var removeTask = store.RemoveAsync(favourite.ListingId);
        store.Clear();
        gate.SetResult();

        Assert.ThrowsAsync<HttpRequestException>(async () => await removeTask);
        Assert.Multiple(() =>
        {
            Assert.That(store.Items, Is.Empty);
            Assert.That(store.IsFavourite(favourite.ListingId), Is.False);
        });
    }

    [Test]
    public async Task Clear_ResetsCachedStateAndForcesReload()
    {
        var favourite = CreateFavourite("Sunny room");
        var apiClient = new FakeApiClient { Favourites = [favourite] };
        var store = new FavouritesStore(apiClient);
        await store.EnsureLoadedAsync();

        store.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(store.Items, Is.Empty);
            Assert.That(store.IsFavourite(favourite.ListingId), Is.False);
        });

        await store.EnsureLoadedAsync();

        Assert.That(apiClient.GetCount, Is.EqualTo(2));
    }

    private static FavouriteListingDto CreateFavourite(string title)
        => new(
            Guid.NewGuid(),
            title,
            1_500_000,
            25,
            1,
            "1 Main St",
            16.0,
            108.0,
            null,
            true,
            DateTime.UtcNow);

    private sealed class FakeApiClient : IApiClient
    {
        public IReadOnlyList<FavouriteListingDto> Favourites { get; set; } = [];

        public int GetCount { get; private set; }

        public List<Guid> Added { get; } = [];

        public List<Guid> Removed { get; } = [];

        public Exception? AddException { get; set; }

        public Exception? RemoveException { get; set; }

        public Task? AddGate { get; set; }

        public Task? GetGate { get; set; }

        public Task? RemoveGate { get; set; }

        public async Task<IReadOnlyList<FavouriteListingDto>> GetFavouritesAsync(
            CancellationToken cancellationToken = default)
        {
            GetCount++;
            if (GetGate is { } gate)
                await gate;

            return Favourites;
        }

        public async Task AddFavouriteAsync(Guid listingId, CancellationToken cancellationToken = default)
        {
            if (AddGate is { } gate)
                await gate;

            if (AddException is { } exception)
                throw exception;

            Added.Add(listingId);
        }

        public async Task RemoveFavouriteAsync(Guid listingId, CancellationToken cancellationToken = default)
        {
            if (RemoveGate is { } gate)
                await gate;

            if (RemoveException is { } exception)
                throw exception;

            Removed.Add(listingId);
        }

        public Task<ListingSearchResultDto> GetListingsAsync(
            int limit,
            string? cursor = null,
            ActiveListingFiltersDto? filters = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ListingDetailFetchResult> GetListingDetailAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ListingSummaryDto>> GetSimilarListingsAsync(
            Guid id,
            int limit = 4,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ListingContactFetchResult> GetListingContactAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<LoginResponse?> DemoLoginAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SearchResultDto<ListingSummaryDto>> SearchByAreaAsync(
            AreaSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SavedSearchDto> CreateSavedSearchAsync(
            CreateSavedSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SavedSearchDto?> SetSavedSearchAlertAsync(
            Guid id,
            SavedSearchAlertState alertState,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteSavedSearchAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
            int limit = 20,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
