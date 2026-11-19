using System.Globalization;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Localization;
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
            Assert.That(vm.SortOptions[0].DisplayName, Is.EqualTo("Mới nhất"));
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
            Assert.That(vm.SortOptions[0].DisplayName, Is.EqualTo("Newest"));
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
            RoomSortOption sortBy = RoomSortOption.Newest,
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
}
