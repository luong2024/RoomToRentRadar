using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.RoomListings.Models;
using RoomToRentRadar.Features.RoomListings.Services;
using RoomToRentRadar.Features.RoomListings.ViewModels;

namespace RoomToRentRadarUnitTests.Features.RoomListings;

[TestFixture]
public class RoomListingsViewModelTests
{
    private FakeRoomListingsService _fakeService = null!;
    private FakeErrorHandler _fakeErrorHandler = null!;
    private List<string> _navigatedRoutes = null!;

    [SetUp]
    public void SetUp()
    {
        _fakeService = new FakeRoomListingsService();
        _fakeErrorHandler = new FakeErrorHandler();
        _navigatedRoutes = [];
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
        vm.SelectSortCommand.Execute(priceAscOption);

        // Wait briefly for the triggered reload task to complete
        await Task.Delay(30);

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
        Assert.That(_navigatedRoutes, Is.EqualTo(new[] { "rooms/details?id=room-ictu-001" }));
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

    private RoomListingsViewModel CreateViewModel(int pageSize) =>
        new(_fakeService, _fakeErrorHandler, route =>
        {
            _navigatedRoutes.Add(route);
            return Task.CompletedTask;
        })
        {
            PageSize = pageSize
        };

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
            RoomSortOption sortBy = RoomSortOption.Newest,
            string? search = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequestedSort = sortBy;
            RequestedSearches.Add(search);

            if (Handler is not null)
            {
                return Handler(page, pageSize, sortBy, cancellationToken);
            }

            return Task.FromResult(new PagedResponse<RoomListItemModel>([], page, pageSize, 0));
        }
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public List<Exception> HandledExceptions { get; } = [];

        public void HandleError(Exception ex) => HandledExceptions.Add(ex);
    }
}
