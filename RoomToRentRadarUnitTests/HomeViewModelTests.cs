using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.Home.ViewModels;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class HomeViewModelTests
{
    [Test]
    public async Task LoadAsync_WhenApiReturnsFirstPage_PopulatesListingsAndPagination()
    {
        var first = CreateListing("First room");
        var second = CreateListing("Second room");
        var apiClient = new FakeApiClient(CreatePage("cursor-2", first, second));
        var viewModel = new HomeViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Listings, Has.Count.EqualTo(2));
            Assert.That(viewModel.Listings[0].Id, Is.EqualTo(first.Id));
            Assert.That(viewModel.ResultsText, Is.EqualTo("2 phòng phù hợp"));
            Assert.That(viewModel.TotalCount, Is.EqualTo(2));
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.IsRefreshing, Is.False);
            Assert.That(viewModel.HasError, Is.False);
            Assert.That(viewModel.CanLoadMore, Is.True);
            Assert.That(apiClient.RequestedLimits, Is.EqualTo(new[] { 12 }));
            Assert.That(apiClient.RequestedCursors, Is.EqualTo(new string?[] { null }));
            Assert.That(apiClient.RequestedFilters, Is.EqualTo(new ActiveListingFiltersDto?[] { null }));
        });
    }

    [Test]
    public async Task LoadAsync_WhenApiThrows_ReportsErrorAndHidesLoadMore()
    {
        var errorHandler = new FakeErrorHandler();
        var viewModel = new HomeViewModel(
            new FakeApiClient(new InvalidOperationException("boom")),
            errorHandler,
            new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Listings, Is.Empty);
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Không thể tải danh sách phòng. Vui lòng thử lại."));
            Assert.That(viewModel.EmptyMessage, Is.EqualTo("Chưa tải được phòng trọ."));
            Assert.That(viewModel.ResultsText, Is.EqualTo("Chưa tải được phòng trọ."));
            Assert.That(viewModel.CanLoadMore, Is.False);
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.IsRefreshing, Is.False);
            Assert.That(errorHandler.HandledException, Is.TypeOf<InvalidOperationException>());
        });
    }

    [Test]
    public async Task LoadMoreAsync_WhenHasMore_AppendsNextPageWithCursor()
    {
        var first = CreateListing("First room");
        var second = CreateListing("Second room");
        var apiClient = new FakeApiClient(
            CreatePage(2, false, "cursor-2", first),
            CreatePage(2, false, null, second));
        var viewModel = new HomeViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.LoadMoreCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Listings, Has.Count.EqualTo(2));
            Assert.That(viewModel.Listings[1].Id, Is.EqualTo(second.Id));
            Assert.That(viewModel.ResultsText, Is.EqualTo("2 phòng phù hợp"));
            Assert.That(viewModel.CanLoadMore, Is.False);
            Assert.That(viewModel.IsLoadingMore, Is.False);
            Assert.That(apiClient.RequestedLimits, Is.EqualTo(new[] { 12, 12 }));
            Assert.That(apiClient.RequestedCursors, Is.EqualTo(new string?[] { null, "cursor-2" }));
        });
    }

    [Test]
    public async Task LoadMoreAsync_WhenPageRepeatsKnownListings_DoesNotDuplicateItems()
    {
        var first = CreateListing("First room");
        var second = CreateListing("Second room");
        var apiClient = new FakeApiClient(
            CreatePage("cursor-2", first),
            CreatePage(null, first, second));
        var viewModel = new HomeViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.LoadMoreCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Listings, Has.Count.EqualTo(2));
            Assert.That(viewModel.ResultsText, Is.EqualTo("2 phòng phù hợp"));
        });
    }

    [Test]
    public async Task LoadMoreAsync_WhenApiThrows_KeepsListingsAndShowsError()
    {
        var first = CreateListing("First room");
        var errorHandler = new FakeErrorHandler();
        var apiClient = new FakeApiClient(
            CreatePage("cursor-2", first),
            new InvalidOperationException("boom"));
        var viewModel = new HomeViewModel(apiClient, errorHandler, new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.LoadMoreCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Listings, Has.Count.EqualTo(1));
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Không thể tải thêm phòng. Vui lòng thử lại."));
            Assert.That(viewModel.CanLoadMore, Is.False);
            Assert.That(viewModel.IsLoadingMore, Is.False);
            Assert.That(errorHandler.HandledException, Is.TypeOf<InvalidOperationException>());
        });
    }

    [Test]
    public async Task RefreshAsync_WhenApiReturnsNewPage_ReplacesListingsAndResetsCursor()
    {
        var first = CreateListing("First room");
        var refreshed = CreateListing("Refreshed room");
        var apiClient = new FakeApiClient(
            CreatePage("cursor-2", first),
            CreatePage(null, refreshed));
        var viewModel = new HomeViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Listings, Has.Count.EqualTo(1));
            Assert.That(viewModel.Listings[0].Id, Is.EqualTo(refreshed.Id));
            Assert.That(viewModel.ResultsText, Is.EqualTo("1 phòng phù hợp"));
            Assert.That(viewModel.CanLoadMore, Is.False);
            Assert.That(viewModel.IsRefreshing, Is.False);
            Assert.That(apiClient.RequestedCursors, Is.EqualTo(new string?[] { null, null }));
        });
    }

    [Test]
    public async Task RefreshAsync_WhenApiThrows_ReportsErrorAndClearsRefreshing()
    {
        var first = CreateListing("First room");
        var errorHandler = new FakeErrorHandler();
        var apiClient = new FakeApiClient(
            CreatePage("cursor-2", first),
            new InvalidOperationException("boom"));
        var viewModel = new HomeViewModel(apiClient, errorHandler, new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Listings, Is.Empty);
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.IsRefreshing, Is.False);
            Assert.That(errorHandler.HandledException, Is.TypeOf<InvalidOperationException>());
        });
    }

    [Test]
    public async Task OpenSelectedListingAsync_WhenListingSelected_NavigatesWithListingIdAndClearsSelection()
    {
        var listing = CreateListing();
        var navigationService = new FakeNavigationService();
        var viewModel = new HomeViewModel(
            new FakeApiClient(CreatePage(null)),
            new FakeErrorHandler(),
            navigationService)
        {
            SelectedListing = HomeListingItem.FromDto(listing)
        };

        await viewModel.OpenSelectedListingCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(navigationService.RequestedListingIds, Has.Count.EqualTo(1));
            Assert.That(navigationService.RequestedListingIds[0], Is.EqualTo(listing.Id));
            Assert.That(viewModel.SelectedListing, Is.Null);
        });
    }

    [Test]
    public async Task OpenSelectedListingAsync_WhenNothingSelected_DoesNotNavigate()
    {
        var navigationService = new FakeNavigationService();
        var viewModel = new HomeViewModel(
            new FakeApiClient(CreatePage(null)),
            new FakeErrorHandler(),
            navigationService);

        await viewModel.OpenSelectedListingCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(navigationService.RequestedListingIds, Is.Empty);
            Assert.That(viewModel.SelectedListing, Is.Null);
        });
    }

    [Test]
    public async Task OpenSelectedListingAsync_WhenNavigationThrows_ReportsErrorAndClearsSelection()
    {
        var listing = CreateListing();
        var exception = new InvalidOperationException("navigation failed");
        var navigationService = new FakeNavigationService { Exception = exception };
        var errorHandler = new FakeErrorHandler();
        var viewModel = new HomeViewModel(
            new FakeApiClient(CreatePage(null)),
            errorHandler,
            navigationService)
        {
            SelectedListing = HomeListingItem.FromDto(listing)
        };

        await viewModel.OpenSelectedListingCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(navigationService.RequestedListingIds, Has.Count.EqualTo(1));
            Assert.That(navigationService.RequestedListingIds[0], Is.EqualTo(listing.Id));
            Assert.That(errorHandler.HandledException, Is.SameAs(exception));
            Assert.That(viewModel.SelectedListing, Is.Null);
        });
    }

    [Test]
    public async Task ApplyFiltersAsync_WithCombinedFilters_SendsEveryFilterAndResetsCursor()
    {
        var apiClient = new FakeApiClient(
            CreatePage("cursor-2", CreateListing("First room")),
            CreatePage(null, CreateListing("Studio near BK")));
        var viewModel = new HomeViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.PriceMinText = "1,5";
        viewModel.PriceMaxText = "3";
        viewModel.AreaMinText = "20";
        viewModel.AreaMaxText = "40";
        viewModel.RoomTypeChips.Single(chip => chip.Label == "Studio").SelectCommand.Execute(null);
        viewModel.CampusChips.Single(chip => chip.Label == "Bách Khoa").SelectCommand.Execute(null);

        await viewModel.ApplyFiltersCommand.ExecuteAsync(null);

        var expectedFilters = new ActiveListingFiltersDto(
            1_500_000,
            3_000_000,
            20,
            40,
            null,
            RoomType.Studio,
            10.7721,
            106.6579,
            3);

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.RequestedFilters, Has.Count.EqualTo(2));
            Assert.That(apiClient.RequestedFilters[1], Is.EqualTo(expectedFilters));
            Assert.That(apiClient.RequestedCursors[1], Is.Null);
            Assert.That(viewModel.IsFilterPanelOpen, Is.False);
            Assert.That(viewModel.HasActiveFilters, Is.True);
            Assert.That(
                viewModel.ActiveFilterChips.Select(chip => chip.Label),
                Is.EqualTo(new[]
                {
                    "Giá 1,5 – 3 triệu",
                    "DT 20 – 40 m²",
                    "Studio",
                    "Bách Khoa · 3 km"
                }));
            Assert.That(viewModel.ResultsText, Is.EqualTo("1 phòng phù hợp"));
        });
    }

    [Test]
    public async Task ApplyFiltersAsync_WithInvalidPriceRange_ShowsErrorAndKeepsResults()
    {
        var apiClient = new FakeApiClient(CreatePage(null, CreateListing()));
        var viewModel = new HomeViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.ToggleFilterPanelCommand.Execute(null);
        viewModel.PriceMinText = "5";
        viewModel.PriceMaxText = "2";

        await viewModel.ApplyFiltersCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.RequestedFilters, Has.Count.EqualTo(1));
            Assert.That(viewModel.HasFilterError, Is.True);
            Assert.That(viewModel.FilterError, Is.EqualTo("Giá tối thiểu không được lớn hơn giá tối đa."));
            Assert.That(viewModel.IsFilterPanelOpen, Is.True);
            Assert.That(viewModel.Listings, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task ClearFiltersAsync_AfterFiltersApplied_RestoresUnfilteredList()
    {
        var fullListRoom = CreateListing("Full list room");
        var filteredRoom = CreateListing("Filtered room");
        var apiClient = new FakeApiClient(
            CreatePage(null, fullListRoom),
            CreatePage(null, filteredRoom),
            CreatePage(null, fullListRoom));
        var viewModel = new HomeViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.RoomTypeChips.Single(chip => chip.Label == "Studio").SelectCommand.Execute(null);
        await viewModel.ApplyFiltersCommand.ExecuteAsync(null);
        await viewModel.ClearFiltersCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.RequestedFilters, Has.Count.EqualTo(3));
            Assert.That(apiClient.RequestedFilters[1], Is.Not.Null);
            Assert.That(apiClient.RequestedFilters[2], Is.Null);
            Assert.That(viewModel.HasActiveFilters, Is.False);
            Assert.That(viewModel.ActiveFilterChips, Is.Empty);
            Assert.That(viewModel.Listings.Select(item => item.Id), Is.EqualTo(new[] { fullListRoom.Id }));
            Assert.That(viewModel.PriceMinText, Is.Empty);
            Assert.That(viewModel.RoomTypeChips.Single(chip => chip.Label == "Tất cả").IsSelected, Is.True);
        });
    }

    [Test]
    public async Task ApplyFiltersAsync_WhenNoMatches_ShowsFilteredEmptyMessage()
    {
        var apiClient = new FakeApiClient(
            CreatePage(null, CreateListing()),
            CreatePage(0, false, null));
        var viewModel = new HomeViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.PriceMaxText = "1";
        await viewModel.ApplyFiltersCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Listings, Is.Empty);
            Assert.That(viewModel.ResultsText, Is.EqualTo("0 phòng phù hợp."));
            Assert.That(viewModel.EmptyMessage, Is.EqualTo("Không tìm thấy phòng phù hợp. Hãy thử nới lỏng bộ lọc."));
        });
    }

    [Test]
    public async Task ApplyFiltersAsync_WithPriceTwoToFourMillionAndBachKhoa_WhenEmpty_ClearFiltersRestoresResults()
    {
        var listing = new ListingSummaryDto(
            Guid.Parse("bd978bb6-3472-46cb-bd2c-743347a82e64"),
            "Restored room", 1_500_000, 25, 1, "1 Main St", 16.0, 108.0,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), null);
        var apiClient = new FakeApiClient(
            CreatePage(2, false, "cursor-2", listing),
            CreatePage(0, false, null),
            CreatePage(2, false, "cursor-2", listing));
        var errorHandler = new FakeErrorHandler();
        var viewModel = new HomeViewModel(apiClient, errorHandler, new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Listings.Select(item => item.Id), Is.EqualTo(new[] { listing.Id }));
            Assert.That(viewModel.CanLoadMore, Is.True);
        });

        viewModel.ToggleFilterPanelCommand.Execute(null);
        viewModel.PriceMinText = "2";
        viewModel.PriceMaxText = "4";
        viewModel.CampusChips.Single(chip => chip.Label == "Bách Khoa").SelectCommand.Execute(null);
        await viewModel.ApplyFiltersCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.RequestedFilters, Is.EqualTo(new ActiveListingFiltersDto?[]
            {
                null,
                new(2_000_000, 4_000_000, null, null, null, null, 10.7721, 106.6579, 3)
            }));
            Assert.That(viewModel.ActiveFilterChips.Select(chip => chip.Label),
                Is.EqualTo(new[] { "Giá 2 – 4 triệu", "Bách Khoa · 3 km" }));
            Assert.That(viewModel.Listings, Is.Empty);
            Assert.That(viewModel.TotalCount, Is.Zero);
            Assert.That(viewModel.ResultsText, Is.EqualTo("0 phòng phù hợp."));
            Assert.That(viewModel.EmptyMessage, Is.EqualTo("Không tìm thấy phòng phù hợp. Hãy thử nới lỏng bộ lọc."));
            Assert.That(viewModel.HasActiveFilters, Is.True);
            Assert.That(viewModel.IsFilterPanelOpen, Is.False);
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.IsRefreshing, Is.False);
            Assert.That(viewModel.IsLoadingMore, Is.False);
            Assert.That(viewModel.HasMore, Is.False);
            Assert.That(viewModel.CanLoadMore, Is.False);
            Assert.That(viewModel.HasError, Is.False);
            Assert.That(viewModel.HasFilterError, Is.False);
        });

        // Repeated collection threshold notifications must not fetch another page while empty.
        for (var attempt = 0; attempt < 3; attempt++)
            await viewModel.LoadMoreCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.RequestedCursors, Is.EqualTo(new string?[] { null, null }));
            Assert.That(viewModel.Listings, Is.Empty);
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.IsRefreshing, Is.False);
            Assert.That(viewModel.IsLoadingMore, Is.False);
            Assert.That(viewModel.CanLoadMore, Is.False);
            Assert.That(errorHandler.HandledException, Is.Null);
        });

        await viewModel.ClearFiltersCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.RequestedFilters, Is.EqualTo(new ActiveListingFiltersDto?[]
            {
                null,
                new(2_000_000, 4_000_000, null, null, null, null, 10.7721, 106.6579, 3),
                null
            }));
            Assert.That(apiClient.RequestedCursors, Is.EqualTo(new string?[] { null, null, null }));
            Assert.That(viewModel.Listings.Select(item => item.Id), Is.EqualTo(new[] { listing.Id }));
            Assert.That(viewModel.TotalCount, Is.EqualTo(2));
            Assert.That(viewModel.ResultsText, Is.EqualTo("2 phòng phù hợp"));
            Assert.That(viewModel.HasActiveFilters, Is.False);
            Assert.That(viewModel.ActiveFilterChips, Is.Empty);
            Assert.That(viewModel.PriceMinText, Is.Empty);
            Assert.That(viewModel.PriceMaxText, Is.Empty);
            Assert.That(viewModel.CampusChips.Where(chip => chip.IsSelected).Select(chip => chip.Label),
                Is.EqualTo(new[] { "Tất cả" }));
            Assert.That(viewModel.IsDistanceEnabled, Is.False);
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.IsRefreshing, Is.False);
            Assert.That(viewModel.IsLoadingMore, Is.False);
            Assert.That(viewModel.CanLoadMore, Is.True);
            Assert.That(viewModel.HasError, Is.False);
            Assert.That(errorHandler.HandledException, Is.Null);
        });
    }

    [Test]
    public async Task LoadMoreAsync_AfterFiltersApplied_SendsSameFiltersWithCursor()
    {
        var first = CreateListing("First filtered");
        var second = CreateListing("Second filtered");
        var apiClient = new FakeApiClient(
            CreatePage(null, CreateListing()),
            CreatePage("cursor-2", first),
            CreatePage(null, second));
        var viewModel = new HomeViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.AreaMaxText = "30";
        await viewModel.ApplyFiltersCommand.ExecuteAsync(null);
        await viewModel.LoadMoreCommand.ExecuteAsync(null);

        var expectedFilters = new ActiveListingFiltersDto(
            null,
            null,
            null,
            30,
            null,
            null,
            null,
            null,
            null);

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.RequestedFilters[2], Is.EqualTo(expectedFilters));
            Assert.That(apiClient.RequestedCursors[2], Is.EqualTo("cursor-2"));
            Assert.That(viewModel.Listings, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task LoadAsync_WhenServerReportsCappedTotal_ShowsCappedResultCount()
    {
        var apiClient = new FakeApiClient(CreatePage(1000, true, null, CreateListing()));
        var viewModel = new HomeViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.TotalCount, Is.EqualTo(1000));
            Assert.That(viewModel.IsTotalCountCapped, Is.True);
            Assert.That(viewModel.ResultsText, Is.EqualTo("1.000+ phòng phù hợp"));
        });
    }

    private static ListingSearchResultDto CreatePage(
        string? nextCursor,
        params ListingSummaryDto[] items)
        => CreatePage(items.Length, false, nextCursor, items);

    private static ListingSearchResultDto CreatePage(
        int totalCount,
        bool totalCountCapped,
        string? nextCursor,
        params ListingSummaryDto[] items)
        => new(
            items,
            new PaginationInfo(12, nextCursor, nextCursor is not null),
            totalCount,
            totalCountCapped,
            null);

    private static ListingSummaryDto CreateListing(string title = "Sunny room")
        => new(
            Guid.NewGuid(),
            title,
            1_500_000,
            25,
            1,
            "1 Main St",
            16.0,
            108.0,
            DateTime.UtcNow,
            null);

    private sealed class FakeApiClient : IApiClient
    {
        private readonly Queue<object> _responses = new();

        public FakeApiClient(params object[] responses)
        {
            foreach (var response in responses)
                _responses.Enqueue(response);
        }

        public List<int> RequestedLimits { get; } = new();

        public List<string?> RequestedCursors { get; } = new();

        public List<ActiveListingFiltersDto?> RequestedFilters { get; } = new();

        public Task<ListingSearchResultDto> GetListingsAsync(
            int limit,
            string? cursor = null,
            ActiveListingFiltersDto? filters = null,
            CancellationToken cancellationToken = default)
        {
            RequestedLimits.Add(limit);
            RequestedCursors.Add(cursor);
            RequestedFilters.Add(filters);

            if (_responses.Count == 0)
                throw new InvalidOperationException("No fake listings response was queued.");

            return _responses.Dequeue() switch
            {
                ListingSearchResultDto page => Task.FromResult(page),
                Exception exception => Task.FromException<ListingSearchResultDto>(exception),
                _ => throw new InvalidOperationException("Unsupported fake listings response.")
            };
        }

        public Task<ListingDetailFetchResult> GetListingDetailAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(new ListingDetailFetchResult(ListingDetailFetchOutcome.NotFound, null));

        public Task<IReadOnlyList<ListingSummaryDto>> GetSimilarListingsAsync(
            Guid id,
            int limit = 4,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The home screen does not load similar listings.");

        public Task<ListingContactFetchResult> GetListingContactAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The home screen does not load listing contacts.");

        public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The home screen does not sign in.");

        public Task<LoginResponse?> DemoLoginAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The home screen does not sign in.");

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The home screen does not sign out.");

        public Task<SearchResultDto<ListingSummaryDto>> SearchByAreaAsync(
            AreaSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The home screen does not search by area.");
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public Exception? HandledException { get; private set; }

        public void HandleError(Exception ex)
        {
            HandledException = ex;
        }
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public List<Guid> RequestedListingIds { get; } = new();

        public Exception? Exception { get; set; }

        public Task GoToRoomDetailAsync(Guid listingId)
        {
            RequestedListingIds.Add(listingId);

            return Exception is { } exception
                ? Task.FromException(exception)
                : Task.CompletedTask;
        }

        public Task GoToLoginAsync() => Task.CompletedTask;

        public Task GoBackAsync() => Task.CompletedTask;
    }
}
