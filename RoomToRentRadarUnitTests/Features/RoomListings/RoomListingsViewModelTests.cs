using System.Globalization;
using Moq;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Localization;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Models.RoomListings;
using RoomToRentRadar.Services.RoomListings;
using RoomToRentRadar.ViewModels.RoomListings;

namespace RoomToRentRadarUnitTests.Features.RoomListings;

[TestFixture]
public class RoomListingsViewModelTests
{
    private FakeRoomListingsService _fakeService = null!;
    private FakeErrorHandler _fakeErrorHandler = null!;
    private List<string> _navigatedRoutes = null!;
    private CultureInfo _initialCulture = null!;

    [SetUp]
    public void SetUp()
    {
        _initialCulture = LocalizationResourceManager.Instance.CurrentCulture;
        LocalizationResourceManager.Instance.SetCulture(new CultureInfo("vi"));
        _fakeService = new FakeRoomListingsService();
        _fakeErrorHandler = new FakeErrorHandler();
        _navigatedRoutes = [];
    }

    [TearDown]
    public void TearDown()
    {
        LocalizationResourceManager.Instance.SetCulture(_initialCulture);
    }

    [Test]
    public async Task LoadInitialRooms_WhenCalled_PopulatesFirstPageAndSetsHasMoreItems()
    {
        // Arrange
        _fakeService.SetupPages(totalCount: 5, pageSize: 2, CreateRooms(1, 5));
        var vm = CreateViewModel(pageSize: 2);

        // Act
        await vm.InitializeCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.Rooms, Has.Count.EqualTo(2));
            Assert.That(vm.Rooms.Select(r => r.Id), Is.EqualTo(new[] { "room-1", "room-2" }));
            Assert.That(vm.CurrentPage, Is.EqualTo(1));
            Assert.That(vm.TotalCount, Is.EqualTo(5));
            Assert.That(vm.HasMoreItems, Is.True);
            Assert.That(vm.IsLoading, Is.False);
            Assert.That(vm.ResultSummaryText, Is.EqualTo("Tìm thấy 5 phòng quanh ICTU"));
        });
    }

    [Test]
    public async Task Initialize_WhenAuthenticated_LoadsFavouriteStateIntoExploreRoom()
    {
        var room = CreateRoom("room-1");
        _fakeService.SetupPages(1, 6, [room]);
        var favourites = new FakeFavouritesService { FavouriteRooms = [CreateRoom("room-1")] };
        var vm = CreateAuthenticatedViewModel(favourites);

        await vm.InitializeCommand.ExecuteAsync(null);

        Assert.That(room.IsFavorite, Is.True);
    }

    [Test]
    public async Task ToggleFavorite_WhenAddSucceeds_PersistsAndUpdatesSharedRoomState()
    {
        var room = CreateRoom("room-1");
        _fakeService.SetupPages(1, 6, [room]);
        var favourites = new FakeFavouritesService();
        var vm = CreateAuthenticatedViewModel(favourites);
        await vm.InitializeCommand.ExecuteAsync(null);

        await vm.ToggleFavoriteCommand.ExecuteAsync(room);

        Assert.Multiple(() =>
        {
            Assert.That(favourites.AddedRoomIds, Is.EqualTo(new[] { "room-1" }));
            Assert.That(room.IsFavorite, Is.True);
            Assert.That(vm.FavoriteCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ToggleFavorite_WhenRemoveSucceeds_UndoRestoresPersistedFavourite()
    {
        var room = CreateRoom("room-1");
        room.IsFavorite = true;
        _fakeService.SetupPages(1, 6, [room]);
        var favourites = new FakeFavouritesService { FavouriteRooms = [CreateRoom("room-1")] };
        var feedback = new FakeFavoriteFeedbackService();
        var vm = CreateAuthenticatedViewModel(favourites, feedback);
        await vm.InitializeCommand.ExecuteAsync(null);

        await vm.ToggleFavoriteCommand.ExecuteAsync(room);
        await feedback.InvokeUndoAsync();

        Assert.Multiple(() =>
        {
            Assert.That(favourites.RemovedRoomIds, Is.EqualTo(new[] { "room-1" }));
            Assert.That(favourites.AddedRoomIds, Is.EqualTo(new[] { "room-1" }));
            Assert.That(room.IsFavorite, Is.True);
            Assert.That(vm.FavoriteCount, Is.EqualTo(1));
            Assert.That(feedback.Messages, Does.Contain("Restored Phòng room-1 to favorites"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Undo_WhenSessionChangesAfterRemoval_DoesNotAddFavourite(bool sameUser)
    {
        var room = CreateRoom("room-1");
        _fakeService.SetupPages(1, 6, [room]);
        var favourites = new FakeFavouritesService { FavouriteRooms = [CreateRoom(room.Id)] };
        var feedback = new FakeFavoriteFeedbackService();
        var session = new Mock<IAuthenticationSessionService>();
        var originalUser = new AuthUser(Guid.NewGuid(), "student@example.com", UserRoleConstants.Student, "LOCAL", true);
        session.SetupGet(item => item.CurrentUser).Returns(originalUser);
        session.SetupGet(item => item.IsAuthenticated).Returns(true);
        session.SetupGet(item => item.IsInitialized).Returns(true);
        session.SetupGet(item => item.SessionEpoch).Returns(1);
        var vm = new RoomListingsViewModel(_fakeService, _fakeErrorHandler, session.Object, favourites, feedback);
        await vm.InitializeAsync();
        await vm.ToggleFavoriteCommand.ExecuteAsync(room);
        Assert.That(favourites.RemovedRoomIds, Is.EqualTo(new[] { room.Id }));

        session.SetupGet(item => item.CurrentUser).Returns((AuthUser?)null);
        session.SetupGet(item => item.IsAuthenticated).Returns(false);
        session.SetupGet(item => item.SessionEpoch).Returns(2);
        session.Raise(item => item.CurrentUserChanged += null!, session.Object, null!);
        var signedInUser = sameUser ? originalUser : originalUser with { Id = Guid.NewGuid() };
        session.SetupGet(item => item.CurrentUser).Returns(signedInUser);
        session.SetupGet(item => item.IsAuthenticated).Returns(true);
        session.SetupGet(item => item.SessionEpoch).Returns(3);
        session.Raise(item => item.CurrentUserChanged += null!, session.Object, signedInUser);
        await feedback.InvokeUndoAsync();

        Assert.Multiple(() =>
        {
            Assert.That(favourites.AddedRoomIds, Is.Empty);
            Assert.That(room.IsFavorite, Is.False);
            Assert.That(vm.FavoriteCount, Is.Zero);
        });
    }

    [Test]
    public async Task ToggleFavorite_WhenAddFails_LeavesFavouriteStateUnchanged()
    {
        var room = CreateRoom("room-1");
        _fakeService.SetupPages(1, 6, [room]);
        var favourites = new FakeFavouritesService { AddException = new HttpRequestException("offline") };
        var vm = CreateAuthenticatedViewModel(favourites);
        await vm.InitializeCommand.ExecuteAsync(null);

        await vm.ToggleFavoriteCommand.ExecuteAsync(room);

        Assert.That(room.IsFavorite, Is.False);
        Assert.That(_fakeErrorHandler.HandledExceptions, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SelectFavoritesTab_ShowsBackendFavouritesAndEmptyState()
    {
        var room = CreateRoom("room-1");
        _fakeService.SetupPages(1, 6, CreateRooms(1, 2));
        var favourites = new FakeFavouritesService { FavouriteRooms = [room] };
        var vm = CreateAuthenticatedViewModel(favourites);
        await vm.InitializeCommand.ExecuteAsync(null);

        await vm.SelectBottomTabCommand.ExecuteAsync("favorites");

        Assert.That(vm.Rooms.Select(item => item.Id), Is.EqualTo(new[] { "room-1" }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LoadMoreRooms_WhenFavoritesIsSelected_DoesNotAppendExploreRooms(bool alreadyInFlight)
    {
        var firstPage = CreateRooms(1, 6);
        var nextPage = new TaskCompletionSource<PagedResponse<RoomListItemModel>>();
        _fakeService.Handler = (page, _, _, _) => page == 1
            ? Task.FromResult(new PagedResponse<RoomListItemModel>(firstPage, 1, 6, 7))
            : nextPage.Task;
        var vm = CreateAuthenticatedViewModel(new FakeFavouritesService { FavouriteRooms = [CreateRoom("room-1")] });
        await vm.InitializeAsync();
        Assert.That(vm.HasMoreItems, Is.True);

        var pending = alreadyInFlight ? vm.LoadMoreRoomsCommand.ExecuteAsync(null) : Task.CompletedTask;
        await vm.SelectBottomTabCommand.ExecuteAsync("favorites");
        Assert.That(vm.IsLoadingMore, Is.False);
        nextPage.SetResult(new PagedResponse<RoomListItemModel>([CreateRoom("room-7")], 2, 6, 7));
        await pending;
        if (!alreadyInFlight)
        {
            await vm.LoadMoreRoomsCommand.ExecuteAsync(null);
        }

        Assert.Multiple(() =>
        {
            Assert.That(vm.Rooms.Select(room => room.Id), Is.EqualTo(new[] { "room-1" }));
            Assert.That(vm.CurrentPage, Is.EqualTo(1));
            Assert.That(vm.IsLoadingMore, Is.False);
            Assert.That(_fakeService.CallCount, Is.EqualTo(alreadyInFlight ? 2 : 1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RefreshFavorites_WhenExploreHasOlderCard_KeepsFavoriteApiDataAfterReloadAndUndo(bool removeFromExplore)
    {
        const string roomId = "room-1";
        var exploreRoom = CreateRoom(roomId, price: 2_000_000m);
        var currentFavorite = new RoomListItemModel
        {
            Id = roomId,
            Title = "Updated room",
            Address = "Updated address",
            Price = 2_500_000m,
            IsAvailable = false
        };
        _fakeService.SetupPages(1, 6, [exploreRoom]);
        var favourites = new FakeFavouritesService { FavouriteRooms = [currentFavorite] };
        var feedback = new FakeFavoriteFeedbackService();
        var vm = CreateAuthenticatedViewModel(favourites, feedback);
        await vm.InitializeAsync();

        await vm.SelectBottomTabCommand.ExecuteAsync("favorites");
        Assert.That(vm.Rooms.Single(), Is.SameAs(currentFavorite));
        await vm.SelectBottomTabCommand.ExecuteAsync("explore");
        await vm.RefreshRoomsCommand.ExecuteAsync(null);
        await vm.SelectBottomTabCommand.ExecuteAsync("favorites");

        Assert.Multiple(() =>
        {
            Assert.That(vm.Rooms.Single().Price, Is.EqualTo(2_500_000m));
            Assert.That(vm.Rooms.Single().IsAvailable, Is.False);
            Assert.That(exploreRoom.IsFavorite, Is.True);
        });

        if (removeFromExplore)
        {
            await vm.SelectBottomTabCommand.ExecuteAsync("explore");
        }
        await vm.ToggleFavoriteCommand.ExecuteAsync(removeFromExplore ? exploreRoom : currentFavorite);
        await vm.SelectBottomTabCommand.ExecuteAsync("favorites");
        Assert.Multiple(() =>
        {
            Assert.That(vm.Rooms, Is.Empty);
            Assert.That(exploreRoom.IsFavorite, Is.False);
        });

        await feedback.InvokeUndoAsync();
        Assert.Multiple(() =>
        {
            Assert.That(vm.Rooms.Single(), Is.SameAs(currentFavorite));
            Assert.That(exploreRoom.IsFavorite, Is.True);
        });
    }

    [Test]
    public async Task SelectExplore_WhenFavoritesLoadFailed_ShowsExploreEmptyState()
    {
        _fakeService.SetupPages(0, 6, []);
        var favourites = new FakeFavouritesService
        {
            Handler = _ => Task.FromException<IReadOnlyList<RoomListItemModel>>(new HttpRequestException("offline"))
        };
        var vm = CreateAuthenticatedViewModel(favourites);
        await vm.InitializeAsync();
        await vm.SelectBottomTabCommand.ExecuteAsync("favorites");
        Assert.That(vm.ShowErrorState, Is.True);
        Assert.That(vm.ErrorMessage, Does.Contain("favorite rooms"));

        await vm.SelectBottomTabCommand.ExecuteAsync("explore");
        await vm.ApplySearchAsync("no match");

        Assert.Multiple(() =>
        {
            Assert.That(vm.FavoriteErrorMessage, Does.Contain("favorite rooms"));
            Assert.That(vm.ShowEmptyState, Is.True);
            Assert.That(vm.ShowLoadingState, Is.False);
            Assert.That(vm.ShowErrorState, Is.False);
            Assert.That(vm.ErrorMessage, Is.Empty);
        });
    }

    [Test]
    public async Task SelectFavorites_WhenExploreLoadIsPendingOrFails_KeepsFavoriteContentVisible()
    {
        var explorePage = new TaskCompletionSource<PagedResponse<RoomListItemModel>>();
        _fakeService.Handler = (_, _, _, _) => explorePage.Task;
        var vm = CreateAuthenticatedViewModel(new FakeFavouritesService { FavouriteRooms = [CreateRoom("saved-room")] });

        var pendingExplore = vm.ApplySearchAsync("test");
        await vm.SelectBottomTabCommand.ExecuteAsync("favorites");
        Assert.That(vm.ShowContentState, Is.True);

        explorePage.SetException(new HttpRequestException("offline"));
        await pendingExplore;

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsFavoritesTabSelected, Is.True);
            Assert.That(vm.Rooms.Select(room => room.Id), Is.EqualTo(new[] { "saved-room" }));
            Assert.That(vm.ShowContentState, Is.True);
            Assert.That(vm.ShowErrorState, Is.False);
            Assert.That(vm.ErrorMessage, Is.Empty);
        });

        await vm.SelectBottomTabCommand.ExecuteAsync("explore");
        Assert.That(vm.ShowErrorState, Is.True);
        Assert.That(vm.ErrorMessage, Is.Not.Empty);
    }

    [Test]
    public async Task RefreshRooms_WhenFavoritesIsSelected_RefreshesOnlyFavoriteRooms()
    {
        _fakeService.SetupPages(1, 6, [CreateRoom("explore-room")]);
        var favourites = new FakeFavouritesService { FavouriteRooms = [CreateRoom("first-favorite")] };
        var vm = CreateAuthenticatedViewModel(favourites);
        await vm.InitializeAsync();
        await vm.SelectBottomTabCommand.ExecuteAsync("favorites");
        favourites.FavouriteRooms = [CreateRoom("updated-favorite")];

        await vm.RefreshRoomsCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsFavoritesTabSelected, Is.True);
            Assert.That(vm.IsRefreshing, Is.False);
            Assert.That(vm.Rooms.Select(room => room.Id), Is.EqualTo(new[] { "updated-favorite" }));
            Assert.That(_fakeService.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RefreshRooms_WhenSwitchingToFavoritesDuringExploreRefresh_KeepsFavoritesVisible()
    {
        var refreshedPage = new TaskCompletionSource<PagedResponse<RoomListItemModel>>();
        _fakeService.SetupPages(1, 6, [CreateRoom("initial-room")]);
        var vm = CreateAuthenticatedViewModel(new FakeFavouritesService { FavouriteRooms = [CreateRoom("saved-room")] });
        await vm.InitializeAsync();
        _fakeService.Handler = (_, _, _, _) => refreshedPage.Task;

        var refresh = vm.RefreshRoomsCommand.ExecuteAsync(null);
        Assert.That(vm.IsRefreshing, Is.True);
        await vm.SelectBottomTabCommand.ExecuteAsync("favorites");
        Assert.That(vm.IsRefreshing, Is.False);

        refreshedPage.SetResult(new PagedResponse<RoomListItemModel>([CreateRoom("updated-room")], 1, 6, 1));
        await refresh;
        Assert.That(vm.IsFavoritesTabSelected, Is.True);
        Assert.That(vm.Rooms.Select(room => room.Id), Is.EqualTo(new[] { "saved-room" }));

        await vm.SelectBottomTabCommand.ExecuteAsync("explore");
        Assert.That(vm.IsRefreshing, Is.False);
        Assert.That(vm.Rooms.Select(room => room.Id), Is.EqualTo(new[] { "updated-room" }));
    }

    [Test]
    public async Task RefreshFavorites_WhenBackendNoLongerReturnsRoom_ClearsExploreCardState()
    {
        var room = CreateRoom("room-1");
        _fakeService.SetupPages(1, 6, [room]);
        var favourites = new FakeFavouritesService { FavouriteRooms = [room] };
        var vm = CreateAuthenticatedViewModel(favourites);
        await vm.InitializeAsync();

        favourites.FavouriteRooms = [];
        await vm.RefreshFavoritesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(room.IsFavorite, Is.False);
            Assert.That(vm.FavoriteCount, Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RefreshFavorites_WhenMutationSucceedsDuringLoad_PreservesLatestFavoriteState(bool initiallySaved)
    {
        var room = CreateRoom("room-1");
        var otherFavorite = CreateRoom("room-2");
        _fakeService.SetupPages(1, 6, [room]);
        var staleResponse = new TaskCompletionSource<IReadOnlyList<RoomListItemModel>>();
        IReadOnlyList<RoomListItemModel> initialFavorites = initiallySaved ? [CreateRoom(room.Id)] : [];
        IReadOnlyList<RoomListItemModel> updatedFavorites = initiallySaved ? [otherFavorite] : [CreateRoom(room.Id), otherFavorite];
        var reads = 0;
        var favourites = new FakeFavouritesService
        {
            Handler = _ => (++reads) switch
            {
                1 => Task.FromResult(initialFavorites),
                2 => staleResponse.Task,
                _ => Task.FromResult(updatedFavorites)
            }
        };
        var vm = CreateAuthenticatedViewModel(favourites);
        await vm.InitializeAsync();
        var refresh = vm.RefreshFavoritesAsync();
        Assert.That(refresh.IsCompleted, Is.False);

        await vm.ToggleFavoriteCommand.ExecuteAsync(room);
        staleResponse.SetResult(initialFavorites);
        await refresh;
        await vm.SelectBottomTabCommand.ExecuteAsync("favorites");

        Assert.Multiple(() =>
        {
            Assert.That(favourites.AddedRoomIds, Has.Count.EqualTo(initiallySaved ? 0 : 1));
            Assert.That(favourites.RemovedRoomIds, Has.Count.EqualTo(initiallySaved ? 1 : 0));
            Assert.That(room.IsFavorite, Is.EqualTo(!initiallySaved));
            Assert.That(vm.Rooms.Select(item => item.Id), Is.EquivalentTo(updatedFavorites.Select(item => item.Id)));
            Assert.That(favourites.GetFavouritesCallCount, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task SelectFavorites_WhileInitializeLoadsFavorites_UsesOneRequestAndDoesNotDuplicateRooms()
    {
        var roomPage = new TaskCompletionSource<PagedResponse<RoomListItemModel>>();
        _fakeService.Handler = (_, pageSize, _, _) => roomPage.Task;
        var favoriteResponse = new TaskCompletionSource<IReadOnlyList<RoomListItemModel>>();
        var favourites = new FakeFavouritesService { Handler = _ => favoriteResponse.Task };
        var vm = CreateAuthenticatedViewModel(favourites);

        var initializeTask = vm.InitializeAsync();
        var selectFavoritesTask = vm.SelectBottomTab("favorites");

        roomPage.SetResult(new PagedResponse<RoomListItemModel>([CreateRoom("room-1")], 1, 6, 1));
        favoriteResponse.SetResult([CreateRoom("room-1")]);
        await Task.WhenAll(initializeTask, selectFavoritesTask);

        Assert.Multiple(() =>
        {
            Assert.That(favourites.GetFavouritesCallCount, Is.EqualTo(1));
            Assert.That(vm.Rooms.Select(room => room.Id), Is.EqualTo(new[] { "room-1" }));
        });
    }

    [Test]
    public async Task SelectFavorites_WhenSessionChangesDuringLoad_DoesNotRestoreOldSessionRooms()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@example.com", UserRoleConstants.Student, "LOCAL", true);
        var session = new Mock<IAuthenticationSessionService>();
        session.SetupGet(item => item.IsAuthenticated).Returns(true);
        session.SetupGet(item => item.CurrentUser).Returns(user);
        session.SetupGet(item => item.SessionEpoch).Returns(1);
        var favoriteResponse = new TaskCompletionSource<IReadOnlyList<RoomListItemModel>>();
        var favourites = new FakeFavouritesService { Handler = _ => favoriteResponse.Task };
        var vm = new RoomListingsViewModel(_fakeService, _fakeErrorHandler, session.Object, favourites);

        var selectTask = vm.SelectBottomTab("favorites");
        session.SetupGet(item => item.IsAuthenticated).Returns(false);
        session.SetupGet(item => item.CurrentUser).Returns((AuthUser?)null);
        session.SetupGet(item => item.SessionEpoch).Returns(2);
        session.Raise(item => item.CurrentUserChanged += null!, session.Object, null!);
        favoriteResponse.SetResult([CreateRoom("old-session-room")]);
        await selectTask;

        Assert.Multiple(() =>
        {
            Assert.That(vm.Rooms, Is.Empty);
            Assert.That(vm.FavoriteCount, Is.Zero);
            Assert.That(vm.IsLoadingFavorites, Is.False);
        });
    }

    [Test]
    public async Task LoadMoreRooms_WhenHasMoreItemsIsTrue_AppendsNextPageWithoutDuplicates()
    {
        // Arrange
        var page1 = new List<RoomListItemModel> { CreateRoom("room-1"), CreateRoom("room-2") };
        var page2WithDuplicate = new List<RoomListItemModel> { CreateRoom("room-2"), CreateRoom("room-3") };

        _fakeService.Handler = (page, pageSize, _, _) =>
        {
            var items = page == 1 ? page1 : page2WithDuplicate;
            return Task.FromResult(new PagedResponse<RoomListItemModel>(items, page, pageSize, TotalCount: 4));
        };

        var vm = CreateViewModel(pageSize: 2);
        await vm.InitializeCommand.ExecuteAsync(null);

        // Act
        await vm.LoadMoreRoomsCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.Rooms, Has.Count.EqualTo(3));
            Assert.That(vm.Rooms.Select(r => r.Id), Is.EqualTo(new[] { "room-1", "room-2", "room-3" }));
            Assert.That(vm.CurrentPage, Is.EqualTo(2));
            Assert.That(vm.HasMoreItems, Is.True);
            Assert.That(vm.IsLoadingMore, Is.False);
        });
    }

    [Test]
    public async Task LoadMoreRooms_WhenReachedTotalCount_SetsHasMoreItemsFalseAndStopsFetching()
    {
        // Arrange
        _fakeService.SetupPages(totalCount: 3, pageSize: 2, CreateRooms(1, 3));
        var vm = CreateViewModel(pageSize: 2);
        await vm.InitializeCommand.ExecuteAsync(null);

        // Act: load page 2 (contains remaining 1 item -> reaches totalCount = 3)
        await vm.LoadMoreRoomsCommand.ExecuteAsync(null);
        var callCountAfterPage2 = _fakeService.CallCount;

        // Try loading more again when HasMoreItems is false
        await vm.LoadMoreRoomsCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.Rooms, Has.Count.EqualTo(3));
            Assert.That(vm.CurrentPage, Is.EqualTo(2));
            Assert.That(vm.HasMoreItems, Is.False);
            Assert.That(_fakeService.CallCount, Is.EqualTo(callCountAfterPage2));
        });
    }

    [Test]
    public async Task LoadMoreRooms_WhenAlreadyLoading_DoesNotTriggerDuplicateServiceCall()
    {
        // Arrange
        var tcs = new TaskCompletionSource<PagedResponse<RoomListItemModel>>();
        _fakeService.Handler = (page, pageSize, _, _) =>
        {
            if (page == 1)
            {
                return Task.FromResult(new PagedResponse<RoomListItemModel>(
                    CreateRooms(1, 2), Page: 1, PageSize: pageSize, TotalCount: 4));
            }

            return tcs.Task;
        };

        var vm = CreateViewModel(pageSize: 2);
        await vm.InitializeCommand.ExecuteAsync(null);

        // Act: start first LoadMore (in-flight), then invoke second LoadMore concurrently
        var firstLoadMoreTask = vm.LoadMoreRoomsCommand.ExecuteAsync(null);
        var secondLoadMoreTask = vm.LoadMoreRoomsCommand.ExecuteAsync(null);

        tcs.SetResult(new PagedResponse<RoomListItemModel>(
            CreateRooms(3, 2), Page: 2, PageSize: 2, TotalCount: 4));

        await Task.WhenAll(firstLoadMoreTask, secondLoadMoreTask);

        // Assert: only 1 initial call + 1 load-more call = 2 total calls
        Assert.Multiple(() =>
        {
            Assert.That(_fakeService.CallCount, Is.EqualTo(2));
            Assert.That(vm.Rooms, Has.Count.EqualTo(4));
        });
    }

    [Test]
    public async Task RefreshRooms_WhenCalled_ResetsToPageOneAndReplacesItemsInPlace()
    {
        // Arrange
        _fakeService.SetupPages(totalCount: 4, pageSize: 2, CreateRooms(1, 4));
        var vm = CreateViewModel(pageSize: 2);
        var originalCollectionInstance = vm.Rooms;

        await vm.InitializeCommand.ExecuteAsync(null);
        await vm.LoadMoreRoomsCommand.ExecuteAsync(null);
        Assert.That(vm.CurrentPage, Is.EqualTo(2));

        // Act
        await vm.RefreshRoomsCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.Rooms, Is.SameAs(originalCollectionInstance));
            Assert.That(vm.CurrentPage, Is.EqualTo(1));
            Assert.That(vm.Rooms, Has.Count.EqualTo(2));
            Assert.That(vm.HasMoreItems, Is.True);
            Assert.That(vm.IsRefreshing, Is.False);
        });
    }

    [Test]
    public async Task SelectedSortOptionChanged_WhenUpdated_ReloadsFromPageOneWithNewSort()
    {
        // Arrange
        _fakeService.SetupPages(totalCount: 4, pageSize: 2, CreateRooms(1, 4));
        var vm = CreateViewModel(pageSize: 2);
        await vm.InitializeCommand.ExecuteAsync(null);
        await vm.LoadMoreRoomsCommand.ExecuteAsync(null);

        var priceAscOption = vm.SortOptions.First(o => o.Option == RoomSortOption.PriceAsc);

        // Act
        await vm.SelectSortCommand.ExecuteAsync(priceAscOption);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_fakeService.LastRequestedSort, Is.EqualTo(RoomSortOption.PriceAsc));
            Assert.That(vm.CurrentPage, Is.EqualTo(1));
            Assert.That(vm.Rooms, Has.Count.EqualTo(2));
            Assert.That(priceAscOption.IsSelected, Is.True);
            Assert.That(vm.SortOptions.Count(o => o.IsSelected), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task LoadMoreRooms_WhenServiceThrowsException_PreservesExistingItemsAndCallsErrorHandler()
    {
        // Arrange
        _fakeService.Handler = (page, pageSize, _, _) =>
        {
            if (page == 1)
            {
                return Task.FromResult(new PagedResponse<RoomListItemModel>(
                    CreateRooms(1, 2), Page: 1, PageSize: pageSize, TotalCount: 4));
            }

            throw new HttpRequestException("Network failure on page 2");
        };

        var vm = CreateViewModel(pageSize: 2);
        await vm.InitializeCommand.ExecuteAsync(null);

        // Act
        await vm.LoadMoreRoomsCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.Rooms, Has.Count.EqualTo(2));
            Assert.That(vm.CurrentPage, Is.EqualTo(1));
            Assert.That(vm.HasMoreItems, Is.True);
            Assert.That(vm.IsLoadingMore, Is.False);
            Assert.That(_fakeErrorHandler.HandledExceptions, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task OpenRoomDetails_WhenValidRoomProvided_NavigatesToRoomDetailsRouteWithId()
    {
        // Arrange
        var vm = CreateViewModel(pageSize: 2);
        var room = CreateRoom("room-ictu-001");

        // Act
        await vm.OpenRoomDetailsCommand.ExecuteAsync(room);

        // Assert
        Assert.That(_navigatedRoutes, Is.EqualTo(new[] { "room-detail?id=room-ictu-001" }));
    }

    [Test]
    public async Task ApplySearch_WhenKeywordProvided_SendsKeywordToServiceAndUsesServerTotals()
    {
        // Arrange: server reports 9 matches across the whole catalog, returned page by page
        _fakeService.SetupPages(totalCount: 9, pageSize: 2, CreateRooms(1, 9));
        var vm = CreateViewModel(pageSize: 2);

        // Act
        await vm.ApplySearchCommand.ExecuteAsync("  điều hòa  ");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.HasSearchQuery, Is.True);
            Assert.That(vm.SearchQuery, Is.EqualTo("điều hòa"));
            Assert.That(_fakeService.RequestedSearches, Is.EqualTo(new[] { "điều hòa" }));
            Assert.That(vm.Rooms, Has.Count.EqualTo(2));
            Assert.That(vm.TotalCount, Is.EqualTo(9));
            Assert.That(vm.HasMoreItems, Is.True);
            Assert.That(vm.ResultSummaryText, Is.EqualTo("Tìm thấy 9 phòng cho \"điều hòa\""));
        });
    }

    [Test]
    public async Task LoadMoreRooms_WhenSearchActive_RequestsNextPageWithSameKeyword()
    {
        // Arrange
        _fakeService.SetupPages(totalCount: 4, pageSize: 2, CreateRooms(1, 4));
        var vm = CreateViewModel(pageSize: 2);
        await vm.ApplySearchCommand.ExecuteAsync("gác xép");

        // Act
        await vm.LoadMoreRoomsCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_fakeService.RequestedSearches, Is.EqualTo(new[] { "gác xép", "gác xép" }));
            Assert.That(vm.Rooms, Has.Count.EqualTo(4));
            Assert.That(vm.CurrentPage, Is.EqualTo(2));
            Assert.That(vm.HasMoreItems, Is.False);
        });
    }

    [Test]
    public async Task ClearSearch_WhenActive_ClearsFilterAndReloadsFullCatalog()
    {
        // Arrange
        _fakeService.SetupPages(totalCount: 4, pageSize: 2, CreateRooms(1, 4));
        var vm = CreateViewModel(pageSize: 2);
        await vm.ApplySearchCommand.ExecuteAsync("room-1");

        // Act
        await vm.ClearSearchCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.HasSearchQuery, Is.False);
            Assert.That(vm.SearchQuery, Is.Empty);
            Assert.That(vm.Rooms, Has.Count.EqualTo(2));
            Assert.That(vm.TotalCount, Is.EqualTo(4));
            Assert.That(vm.HasMoreItems, Is.True);
            Assert.That(_fakeService.RequestedSearches[^1], Is.Null);
        });
    }

    [Test]
    public async Task LoadFirstPage_WhenSupersededByNewerRequest_KeepsIsLoadingTrueUntilNewerRequestCompletes()
    {
        // Arrange
        var firstRequestTcs = new TaskCompletionSource<PagedResponse<RoomListItemModel>>();
        var secondRequestTcs = new TaskCompletionSource<PagedResponse<RoomListItemModel>>();
        var callIndex = 0;

        _fakeService.Handler = (_, _, _, cancellationToken) =>
        {
            callIndex++;
            if (callIndex == 1)
            {
                cancellationToken.Register(() => firstRequestTcs.TrySetCanceled(cancellationToken));
                return firstRequestTcs.Task;
            }

            return secondRequestTcs.Task;
        };

        var vm = CreateViewModel(pageSize: 2);

        // Act: start first request, then supersede it with a second search request
        var firstTask = vm.ApplySearchCommand.ExecuteAsync("z115");
        var secondTask = vm.ApplySearchCommand.ExecuteAsync("quyết thắng");

        await firstTask;

        // Assert while second request is still in-flight: canceled first request must NOT reset IsLoading
        Assert.That(vm.IsLoading, Is.True);

        // Complete second request and verify final state
        secondRequestTcs.SetResult(new PagedResponse<RoomListItemModel>(
            CreateRooms(1, 2), Page: 1, PageSize: 2, TotalCount: 2));
        await secondTask;

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsLoading, Is.False);
            Assert.That(vm.Rooms, Has.Count.EqualTo(2));
            Assert.That(vm.SearchQuery, Is.EqualTo("quyết thắng"));
        });
    }

    [Test]
    public async Task LoadingState_WhileInitialLoadInFlight_ShowsOnlyLoadingState()
    {
        var loadTcs = new TaskCompletionSource<PagedResponse<RoomListItemModel>>();
        _fakeService.Handler = (_, _, _, _) => loadTcs.Task;
        var vm = CreateViewModel(pageSize: 2);

        var loadTask = vm.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(vm.ShowLoadingState, Is.True);
            Assert.That(vm.ShowEmptyState, Is.False);
            Assert.That(vm.ShowErrorState, Is.False);
            Assert.That(vm.ShowContentState, Is.False);
        });

        loadTcs.SetResult(new PagedResponse<RoomListItemModel>(CreateRooms(1, 2), 1, 2, 2));
        await loadTask;

        Assert.Multiple(() =>
        {
            Assert.That(vm.ShowLoadingState, Is.False);
            Assert.That(vm.ShowEmptyState, Is.False);
            Assert.That(vm.ShowErrorState, Is.False);
            Assert.That(vm.ShowContentState, Is.True);
        });
    }

    [Test]
    public async Task EmptyState_WhenInitialLoadReturnsZeroRooms_ShowsOnlyEmptyState()
    {
        _fakeService.SetupPages(totalCount: 0, pageSize: 2, allRooms: []);
        var vm = CreateViewModel(pageSize: 2);

        await vm.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(vm.ShowLoadingState, Is.False);
            Assert.That(vm.ShowEmptyState, Is.True);
            Assert.That(vm.ShowErrorState, Is.False);
            Assert.That(vm.ShowContentState, Is.False);
        });
    }

    [Test]
    public async Task ErrorState_WhenInitialLoadThrowsHttpRequestException_ShowsOnlyErrorStateAndSetsMessage()
    {
        _fakeService.Handler = (_, _, _, _) => throw new HttpRequestException("Network failure");
        var vm = CreateViewModel(pageSize: 2);

        await vm.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(vm.ShowLoadingState, Is.False);
            Assert.That(vm.ShowEmptyState, Is.False);
            Assert.That(vm.ShowErrorState, Is.True);
            Assert.That(vm.ShowContentState, Is.False);
            Assert.That(vm.ErrorMessage, Does.Contain("kết nối"));
            Assert.That(_fakeErrorHandler.HandledExceptions, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task RetryCommand_WhenErrorOccurred_RetriesServiceCallAndRecovers()
    {
        var shouldFail = true;
        _fakeService.Handler = (_, _, _, _) =>
        {
            if (shouldFail)
            {
                throw new HttpRequestException("Network failure");
            }

            return Task.FromResult(new PagedResponse<RoomListItemModel>(CreateRooms(1, 2), 1, 2, 2));
        };

        var vm = CreateViewModel(pageSize: 2);
        await vm.InitializeAsync();

        Assert.That(vm.ShowErrorState, Is.True);

        shouldFail = false;
        await vm.RetryCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.ShowErrorState, Is.False);
            Assert.That(vm.ShowContentState, Is.True);
            Assert.That(vm.Rooms, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task ResetFiltersCommand_WhenExecuted_ClearsSearchAndReloadsCatalog()
    {
        _fakeService.SetupPages(totalCount: 2, pageSize: 2, allRooms: CreateRooms(1, 2));
        var vm = CreateViewModel(pageSize: 2);
        vm.SearchQuery = "Phòng trọ cao cấp";

        await vm.ResetFiltersCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.SearchQuery, Is.Empty);
            Assert.That(vm.ShowContentState, Is.True);
            Assert.That(vm.Rooms, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void CultureChange_WhenSwitchedToEnglish_UpdatesTitleEmptyMessageSortOptionsAndSummaryText()
    {
        // Arrange
        var vm = CreateViewModel(pageSize: 6);
        vm.TotalCount = 12;

        Assert.Multiple(() =>
        {
            Assert.That(vm.Title, Is.EqualTo("Khám phá phòng trọ"));
            Assert.That(vm.EmptyStateMessage, Is.EqualTo("Chưa có phòng trọ nào phù hợp."));
            Assert.That(vm.ResultSummaryText, Is.EqualTo("Tìm thấy 12 phòng quanh ICTU"));
            Assert.That(vm.SortOptions[0].DisplayName, Is.EqualTo("Gần ICTU nhất"));
            Assert.That(vm.PriceSortLabel, Is.EqualTo("Giá"));
        });

        // Act
        LocalizationResourceManager.Instance.SetCulture(new CultureInfo("en"));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.Title, Is.EqualTo("Explore Rooms"));
            Assert.That(vm.EmptyStateMessage, Is.EqualTo("No matching rooms found."));
            Assert.That(vm.ResultSummaryText, Is.EqualTo("Found 12 rooms around ICTU"));
            Assert.That(vm.SortOptions[0].DisplayName, Is.EqualTo("Closest to ICTU"));
            Assert.That(vm.PriceSortLabel, Is.EqualTo("Price"));
        });
    }

    [Test]
    public void CultureChange_WhenPriceSortSelectedAndSwitched_UpdatesPriceSortLabel()
    {
        // Arrange
        var vm = CreateViewModel(pageSize: 6);
        var priceAscOption = vm.SortOptions.First(o => o.Option == RoomSortOption.PriceAsc);
        vm.SelectedSortOptionItem = priceAscOption;

        Assert.That(vm.PriceSortLabel, Is.EqualTo("Giá: Thấp đến Cao"));

        // Act
        LocalizationResourceManager.Instance.SetCulture(new CultureInfo("en"));

        // Assert
        Assert.That(vm.PriceSortLabel, Is.EqualTo("Price: Low to High"));
    }

    [Test]
    public async Task ApplyFilterSheetCommand_WhenExecuted_PassesFilterCriteriaToService()
    {
        // Arrange
        _fakeService.SetupPages(totalCount: 3, pageSize: 2, allRooms: CreateRooms(1, 3));
        var vm = CreateViewModel(pageSize: 2);
        vm.SelectPricePreset("2m-3m");
        vm.SelectRoomType("shared");
        vm.ToggleAmenityFilter("wifi");
        vm.ToggleAmenityFilter("kitchen");

        // Act
        await vm.ApplyFilterSheetCommand.ExecuteAsync(null);

        // Assert
        var appliedFilter = _fakeService.LastRequestedFilter;
        Assert.Multiple(() =>
        {
            Assert.That(appliedFilter, Is.Not.Null);
            Assert.That(appliedFilter!.MinPrice, Is.EqualTo(2_000_000m));
            Assert.That(appliedFilter.MaxPrice, Is.EqualTo(3_000_000m));
            Assert.That(appliedFilter.RoomType, Is.EqualTo("shared"));
            Assert.That(appliedFilter.Amenities, Is.EqualTo(new[] { "wifi", "kitchen" }));
            Assert.That(vm.IsPriceMenuOpen, Is.False);
            Assert.That(vm.Rooms, Has.Count.EqualTo(2));
            Assert.That(vm.TotalCount, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task ResetFiltersAsync_WhenExecuted_ClearsAllFilterCriteriaAndClearsActiveFilters()
    {
        // Arrange
        _fakeService.SetupPages(totalCount: 2, pageSize: 2, allRooms: CreateRooms(1, 2));
        var vm = CreateViewModel(pageSize: 2);

        // Mutate filters away from unfiltered state
        vm.SelectPricePreset("1m-2m");
        vm.SelectedRoomType = "private";
        vm.FilterMinAreaText = "20";
        vm.FilterMaxAreaText = "40";
        vm.IsDistanceFilterActive = true;
        vm.RadiusInKm = 3.0;
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        Assert.That(vm.ActiveFilters, Is.Not.Empty);

        // Act: reset filters
        await vm.ResetFiltersCommand.ExecuteAsync(null);

        // Assert reset state is completely unfiltered (no hidden defaults sent to service/API)
        Assert.Multiple(() =>
        {
            Assert.That(vm.SelectedPricePreset, Is.Empty);
            Assert.That(vm.FilterMinPriceText, Is.Empty);
            Assert.That(vm.FilterMaxPriceText, Is.Empty);
            Assert.That(vm.MinPrice, Is.Null);
            Assert.That(vm.MaxPrice, Is.Null);
            Assert.That(vm.FilterMinAreaText, Is.Empty);
            Assert.That(vm.FilterMaxAreaText, Is.Empty);
            Assert.That(vm.MinArea, Is.Null);
            Assert.That(vm.MaxArea, Is.Null);
            Assert.That(vm.SelectedRoomType, Is.EqualTo("all"));
            Assert.That(vm.IsDistanceFilterActive, Is.False);
            Assert.That(vm.ActiveFilters, Is.Empty);
            Assert.That(vm.HasActiveFilters, Is.False);
            Assert.That(_fakeService.LastRequestedFilter?.MinPrice, Is.Null);
            Assert.That(_fakeService.LastRequestedFilter?.MaxPrice, Is.Null);
            Assert.That(_fakeService.LastRequestedFilter?.RoomType, Is.Null);
        });
    }

    [Test]
    public async Task ErrorState_WhenEnglishSelectedAndSwitchCulture_LocalizesErrorMessage()
    {
        // Arrange
        LocalizationResourceManager.Instance.SetCulture(new CultureInfo("en"));
        _fakeService.Handler = (_, _, _, _) => throw new HttpRequestException("Network failure");
        var vm = CreateViewModel(pageSize: 2);

        // Act
        await vm.InitializeAsync();

        // Assert English error message
        Assert.Multiple(() =>
        {
            Assert.That(vm.ShowErrorState, Is.True);
            Assert.That(vm.ErrorMessage, Is.EqualTo("Cannot connect to server. Please check your network connection."));
            Assert.That(vm.EmptyStateMessage, Is.EqualTo("Cannot connect to server. Please check your network connection."));
        });

        // Act: switch back to Vietnamese while error is shown
        LocalizationResourceManager.Instance.SetCulture(new CultureInfo("vi"));

        // Assert Vietnamese error message
        Assert.Multiple(() =>
        {
            Assert.That(vm.ErrorMessage, Is.EqualTo("Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối mạng."));
            Assert.That(vm.EmptyStateMessage, Is.EqualTo("Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối mạng."));
        });
    }

    [Test]
    public void IsLandlord_WhenNoSessionService_IsFalse()
    {
        var vm = CreateViewModel(pageSize: 6);
        Assert.That(vm.IsLandlord, Is.False);
    }

    [Test]
    public void IsLandlord_WhenCurrentUserIsLandlord_IsTrue()
    {
        var mockSessionService = new Mock<IAuthenticationSessionService>();
        var landlord = new AuthUser(Guid.NewGuid(), "landlord@example.com", UserRoleConstants.Landlord, "LOCAL", true);
        mockSessionService.Setup(s => s.CurrentUser).Returns(landlord);

        var vm = new RoomListingsViewModel(_fakeService, _fakeErrorHandler, mockSessionService.Object);

        Assert.That(vm.IsLandlord, Is.True);
    }

    [Test]
    public void IsLandlord_WhenCurrentUserIsStudent_IsFalse()
    {
        var mockSessionService = new Mock<IAuthenticationSessionService>();
        var student = new AuthUser(Guid.NewGuid(), "student@example.com", UserRoleConstants.Student, "LOCAL", true);
        mockSessionService.Setup(s => s.CurrentUser).Returns(student);

        var vm = new RoomListingsViewModel(_fakeService, _fakeErrorHandler, mockSessionService.Object);

        Assert.That(vm.IsLandlord, Is.False);
    }

    [Test]
    public void IsLandlord_WhenCurrentUserChangedToLandlord_UpdatesToTrue()
    {
        var mockSessionService = new Mock<IAuthenticationSessionService>();
        mockSessionService.Setup(s => s.CurrentUser).Returns((AuthUser?)null);

        var vm = new RoomListingsViewModel(_fakeService, _fakeErrorHandler, mockSessionService.Object);
        Assert.That(vm.IsLandlord, Is.False);

        var landlord = new AuthUser(Guid.NewGuid(), "landlord@example.com", UserRoleConstants.Landlord, "LOCAL", true);
        mockSessionService.Raise(s => s.CurrentUserChanged += null, mockSessionService.Object, landlord);

        Assert.That(vm.IsLandlord, Is.True);
    }

    [Test]
    public void IsLandlord_WhenLandlordLogsOut_UpdatesToFalse()
    {
        var mockSessionService = new Mock<IAuthenticationSessionService>();
        var landlord = new AuthUser(Guid.NewGuid(), "landlord@example.com", UserRoleConstants.Landlord, "LOCAL", true);
        mockSessionService.Setup(s => s.CurrentUser).Returns(landlord);

        var vm = new RoomListingsViewModel(_fakeService, _fakeErrorHandler, mockSessionService.Object);
        Assert.That(vm.IsLandlord, Is.True);

        mockSessionService.Raise(s => s.CurrentUserChanged += null, mockSessionService.Object, null!);

        Assert.That(vm.IsLandlord, Is.False);
    }

    private RoomListingsViewModel CreateViewModel(int pageSize) =>
        new(_fakeService, _fakeErrorHandler, route =>
        {
            _navigatedRoutes.Add(route);
            return Task.CompletedTask;
        })
        {
            PageSize = pageSize
        };

    private RoomListingsViewModel CreateAuthenticatedViewModel(
        FakeFavouritesService favourites,
        FakeFavoriteFeedbackService? feedback = null)
    {
        var session = new Mock<IAuthenticationSessionService>();
        session.SetupGet(item => item.IsAuthenticated).Returns(true);
        session.SetupGet(item => item.IsInitialized).Returns(true);
        session.SetupGet(item => item.CurrentUser)
            .Returns(new AuthUser(Guid.NewGuid(), "student@example.com", UserRoleConstants.Student, "LOCAL", true));
        return new RoomListingsViewModel(_fakeService, _fakeErrorHandler, session.Object, favourites, feedback ?? new FakeFavoriteFeedbackService());
    }

    private static List<RoomListItemModel> CreateRooms(int startIndex, int count) =>
        Enumerable.Range(startIndex, count)
            .Select(i => CreateRoom($"room-{i}", price: 1_500_000m + (i * 100_000m)))
            .ToList();

    private static RoomListItemModel CreateRoom(string id, decimal price = 1_800_000m) =>
        new()
        {
            Id = id,
            Title = $"Phòng {id}",
            Price = price,
            AreaSqm = 20,
            Address = "Đường Z115, Quyết Thắng",
            DistanceToCampusKm = 0.5,
            IsVerified = true
        };

    private sealed class FakeRoomListingsService : IRoomListingsService
    {
        public int CallCount { get; private set; }

        public RoomSortOption LastRequestedSort { get; private set; }

        public RoomFilterCriteria? LastRequestedFilter { get; private set; }

        public List<string?> RequestedSearches { get; } = [];

        public Func<int, int, RoomSortOption, CancellationToken, Task<PagedResponse<RoomListItemModel>>>? Handler { get; set; }

        public void SetupPages(int totalCount, int pageSize, IReadOnlyList<RoomListItemModel> allRooms)
        {
            Handler = (page, size, _, _) =>
            {
                var items = allRooms.Skip((page - 1) * size).Take(size).ToList();
                return Task.FromResult(new PagedResponse<RoomListItemModel>(items, page, size, totalCount));
            };
        }

        public Task<PagedResponse<RoomListItemModel>> GetRoomListingsAsync(
            int page,
            int pageSize,
            RoomSortOption sortBy = RoomSortOption.DistanceAsc,
            string? search = null,
            RoomFilterCriteria? filter = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequestedSort = sortBy;
            LastRequestedFilter = filter;
            RequestedSearches.Add(search);

            if (Handler is not null)
            {
                return Handler(page, pageSize, sortBy, cancellationToken);
            }

            return Task.FromResult(new PagedResponse<RoomListItemModel>([], page, pageSize, 0));
        }

        public Task<RoomListItemModel?> GetRoomByIdAsync(
            string roomId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<RoomListItemModel?>(CreateRoom(roomId));
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public List<Exception> HandledExceptions { get; } = [];

        public void HandleError(Exception ex) => HandledExceptions.Add(ex);
    }

    private sealed class FakeFavouritesService : IFavouritesService
    {
        public IReadOnlyList<RoomListItemModel> FavouriteRooms { get; set; } = [];
        public List<string> AddedRoomIds { get; } = [];
        public List<string> RemovedRoomIds { get; } = [];
        public Exception? AddException { get; init; }
        public Func<CancellationToken, Task<IReadOnlyList<RoomListItemModel>>>? Handler { get; init; }
        public int GetFavouritesCallCount { get; private set; }
        public Task<IReadOnlyList<RoomListItemModel>> GetMyFavouritesAsync(CancellationToken cancellationToken = default)
        {
            GetFavouritesCallCount++;
            return Handler?.Invoke(cancellationToken) ?? Task.FromResult(FavouriteRooms);
        }
        public Task<IReadOnlySet<string>> GetMyFavouriteIdsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<string>>(FavouriteRooms.Select(room => room.Id).ToHashSet(StringComparer.Ordinal));
        public Task AddFavouriteAsync(string roomId, CancellationToken cancellationToken = default)
        {
            if (AddException is not null) throw AddException;
            AddedRoomIds.Add(roomId);
            return Task.CompletedTask;
        }
        public Task RemoveFavouriteAsync(string roomId, CancellationToken cancellationToken = default)
        {
            RemovedRoomIds.Add(roomId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFavoriteFeedbackService : IFavoriteFeedbackService
    {
        private Func<Task>? _undo;
        public List<string> Messages { get; } = [];
        public Task ShowMessageAsync(string message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
        public Task ShowUndoAsync(string message, string actionText, Func<Task> action)
        {
            _undo = action;
            return Task.CompletedTask;
        }
        public Task InvokeUndoAsync() => _undo?.Invoke() ?? Task.CompletedTask;
    }
}
