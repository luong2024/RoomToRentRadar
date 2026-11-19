using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.Comparison.Models;
using RoomToRentRadar.Features.Comparison.Services;
using RoomToRentRadar.Features.Comparison.ViewModels;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Services;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Notifications;
using RoomToRentRadar.Shared.SavedSearches;
using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ComparisonViewModelTests
{
    [Test]
    public async Task LoadAsync_WithTwoRooms_BuildsSixRowsAndHighlightsBestValues()
    {
        var cheaper = Guid.NewGuid();
        var roomier = Guid.NewGuid();
        var apiClient = new FakeApiClient(
            CreateDetail(cheaper, price: 3_000_000, deposit: 3_000_000, area: 20, AmenityCode.Wifi),
            CreateDetail(roomier, price: 2_000_000, deposit: 2_000_000, area: 30,
                AmenityCode.Wifi, AmenityCode.Security, AmenityCode.Parking));
        var travelClient = new FakeTravelEstimatesClient();
        travelClient.Estimates[cheaper] = Found(Motorbike(800));
        travelClient.Estimates[roomier] = Found(Motorbike(1500));
        var viewModel = CreateViewModel(CreateStore(cheaper, roomier), apiClient, travelClient, campus: "Bách Khoa");

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Columns, Has.Count.EqualTo(2));
            Assert.That(viewModel.Rows.Select(row => row.Label), Is.EqualTo(new[]
            {
                "Giá thuê", "Tiền cọc", "Diện tích", "Khoảng cách", "Tiện nghi", "Điểm an ninh"
            }));
            Assert.That(viewModel.Rows[0].Cells.Select(cell => cell.Text), Is.EqualTo(new[] { "3.000.000 đ", "2.000.000 đ" }));
            Assert.That(viewModel.Rows[0].Cells.Select(cell => cell.IsBest), Is.EqualTo(new[] { false, true }));
            Assert.That(viewModel.Rows[1].Cells.Select(cell => cell.IsBest), Is.EqualTo(new[] { false, true }));
            Assert.That(viewModel.Rows[2].Cells.Select(cell => cell.Text), Is.EqualTo(new[] { "20 m²", "30 m²" }));
            Assert.That(viewModel.Rows[2].Cells.Select(cell => cell.IsBest), Is.EqualTo(new[] { false, true }));
            Assert.That(viewModel.Rows[3].Cells.Select(cell => cell.Text), Is.EqualTo(new[] { "800 m", "1,5 km" }));
            Assert.That(viewModel.Rows[3].Cells.Select(cell => cell.IsBest), Is.EqualTo(new[] { true, false }));
            Assert.That(viewModel.Rows[4].Cells.Select(cell => cell.IsBest), Is.EqualTo(new[] { false, true }));
            Assert.That(viewModel.Rows[4].Cells[1].Text,
                Is.EqualTo("3 tiện nghi\nWi-Fi · An ninh +1"));
            Assert.That(viewModel.Rows[5].Cells.Select(cell => cell.Text),
                Is.EqualTo(new[] { "40/100\nCần cân nhắc", "77/100\nAn toàn" }));
            Assert.That(viewModel.Rows[5].Cells.Select(cell => cell.IsBest), Is.EqualTo(new[] { false, true }));
            Assert.That(viewModel.ComparisonSummary, Is.EqualTo("So sánh 2 phòng · 6 tiêu chí"));
            Assert.That(viewModel.DistanceNote, Is.Empty);
            Assert.That(viewModel.HasError, Is.False);
            Assert.That(viewModel.ShowContent, Is.True);
        });
    }

    [Test]
    public async Task LoadAsync_WithoutCampus_ShowsPromptInsteadOfDistance()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var apiClient = new FakeApiClient(
            CreateDetail(first, 3_000_000, 3_000_000, 20, AmenityCode.Wifi),
            CreateDetail(second, 2_000_000, 2_000_000, 30, AmenityCode.Wifi));
        var travelClient = new FakeTravelEstimatesClient();
        var viewModel = CreateViewModel(CreateStore(first, second), apiClient, travelClient, campus: null);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Rows[3].Cells.Select(cell => cell.Text), Is.EqualTo(new[] { "Chọn khu vực", "Chọn khu vực" }));
            Assert.That(viewModel.DistanceNote, Is.EqualTo("Chọn khu vực của bạn để so sánh khoảng cách."));
            Assert.That(viewModel.HasDistanceNote, Is.True);
            Assert.That(travelClient.RequestedListingIds, Is.Empty);
        });
    }

    [Test]
    public async Task LoadAsync_WhenTravelIsRateLimited_ShowsDashAndRateLimitNote()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var apiClient = new FakeApiClient(
            CreateDetail(first, 3_000_000, 3_000_000, 20, AmenityCode.Wifi),
            CreateDetail(second, 2_000_000, 2_000_000, 30, AmenityCode.Wifi));
        var travelClient = new FakeTravelEstimatesClient();
        travelClient.Estimates[first] = new TravelEstimatesFetchResult(TravelEstimatesFetchOutcome.RateLimited, null);
        travelClient.Estimates[second] = Found(Motorbike(1200));
        var viewModel = CreateViewModel(CreateStore(first, second), apiClient, travelClient, campus: "Bách Khoa");

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Rows[3].Cells.Select(cell => cell.Text), Is.EqualTo(new[] { "—", "1,2 km" }));
            Assert.That(viewModel.DistanceNote, Does.Contain("giới hạn tính đường"));
        });
    }

    [Test]
    public async Task LoadAsync_WhenTravelFallsBackToStraightLine_MarksDistanceWithAsterisk()
    {
        var first = Guid.NewGuid();
        var apiClient = new FakeApiClient(
            CreateDetail(first, 3_000_000, 3_000_000, 20, AmenityCode.Wifi));
        var travelClient = new FakeTravelEstimatesClient();
        travelClient.Estimates[first] = Found(new TravelEstimateDto(TravelMode.Motorbike, TravelSource.Fallback, 1500, null));
        var viewModel = CreateViewModel(CreateStore(first), apiClient, travelClient, campus: "Bách Khoa");

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Rows[3].Cells.Select(cell => cell.Text), Is.EqualTo(new[] { "1,5 km*" }));
            Assert.That(viewModel.DistanceNote, Does.Contain("dấu *"));
        });
    }

    [Test]
    public async Task LoadAsync_WhenAListingIsGone_ShowsUnavailableColumnAndNotesPartialComparison()
    {
        var available = Guid.NewGuid();
        var removed = Guid.NewGuid();
        var apiClient = new FakeApiClient(CreateDetail(available, 2_000_000, 2_000_000, 25, AmenityCode.Wifi));
        apiClient.Missing.Add(removed);
        var viewModel = CreateViewModel(CreateStore(available, removed), apiClient, new FakeTravelEstimatesClient(), campus: "Bách Khoa");

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Columns, Has.Count.EqualTo(2));
            Assert.That(viewModel.Columns[1].PriceText, Is.EqualTo("Không còn khả dụng"));
            Assert.That(viewModel.Rows[0].Cells.Select(cell => cell.Text), Is.EqualTo(new[] { "2.000.000 đ", "—" }));
            Assert.That(viewModel.Rows[0].Cells[1].IsBest, Is.False);
            Assert.That(viewModel.DistanceNote, Does.Contain("không còn khả dụng"));
            Assert.That(viewModel.HasError, Is.False);
        });
    }

    [Test]
    public async Task LoadAsync_WhenEveryListingFails_ShowsErrorState()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var exception = new InvalidOperationException("network down");
        var apiClient = new FakeApiClient { Exception = exception };
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(
            CreateStore(first, second),
            apiClient,
            new FakeTravelEstimatesClient(),
            campus: "Bách Khoa",
            errorHandler: errorHandler);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Chưa tải được dữ liệu so sánh. Kiểm tra kết nối rồi thử lại."));
            Assert.That(viewModel.Columns, Is.Empty);
            Assert.That(viewModel.Rows, Is.Empty);
            Assert.That(viewModel.ShowEmptyState, Is.False);
            Assert.That(viewModel.ShowContent, Is.False);
            Assert.That(viewModel.IsLoading, Is.False);
            Assert.That(errorHandler.HandledException, Is.TypeOf<InvalidOperationException>());
        });
    }

    [Test]
    public async Task RemoveRoom_WhenLastRoomRemoved_FallsBackToEmptyState()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var apiClient = new FakeApiClient(
            CreateDetail(first, 3_000_000, 3_000_000, 20, AmenityCode.Wifi),
            CreateDetail(second, 2_000_000, 2_000_000, 30, AmenityCode.Wifi));
        var store = CreateStore(first, second);
        var viewModel = CreateViewModel(store, apiClient, new FakeTravelEstimatesClient(), campus: "Bách Khoa");
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.RemoveRoomCommand.ExecuteAsync(second);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Columns.Select(column => column.Id), Is.EqualTo(new[] { first }));
            Assert.That(viewModel.Rows[0].Cells, Has.Count.EqualTo(1));
            Assert.That(viewModel.Rows[0].Cells[0].IsBest, Is.False);
        });

        await viewModel.ClearAllCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(store.Rooms, Is.Empty);
            Assert.That(viewModel.Columns, Is.Empty);
            Assert.That(viewModel.Rows, Is.Empty);
            Assert.That(viewModel.ShowEmptyState, Is.True);
            Assert.That(viewModel.HasError, Is.False);
        });
    }

    [Test]
    public async Task LoadAsync_WithSingleRoom_NeverHighlightsCells()
    {
        var only = Guid.NewGuid();
        var apiClient = new FakeApiClient(CreateDetail(only, 3_000_000, 3_000_000, 20, AmenityCode.Wifi));
        var travelClient = new FakeTravelEstimatesClient();
        travelClient.Estimates[only] = Found(Motorbike(800));
        var viewModel = CreateViewModel(CreateStore(only), apiClient, travelClient, campus: "Bách Khoa");

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Rows, Has.Count.EqualTo(6));
            Assert.That(viewModel.Rows.All(row => row.Cells.All(cell => !cell.IsBest)), Is.True);
            Assert.That(viewModel.ComparisonSummary, Is.EqualTo("So sánh 1 phòng · 6 tiêu chí"));
        });
    }

    [Test]
    public async Task OnAppearing_LoadsStoredCampusAndBuildsColumns()
    {
        var first = Guid.NewGuid();
        var apiClient = new FakeApiClient(CreateDetail(first, 3_000_000, 3_000_000, 20, AmenityCode.Wifi));
        var campusStore = new FakeCampusSelectionStore { SelectedCampus = "KHTN" };
        var viewModel = new ComparisonViewModel(
            CreateStore(first),
            apiClient,
            new FakeTravelEstimatesClient(),
            campusStore,
            new FakeErrorHandler(),
            new FakeNavigationService());

        viewModel.OnAppearing();
        await WaitForIdleAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedCampusName, Is.EqualTo("KHTN"));
            Assert.That(viewModel.HasCampusSelection, Is.True);
            Assert.That(viewModel.HasColumns, Is.True);
            Assert.That(viewModel.CampusNames, Is.EqualTo(new[] { "Bách Khoa", "KHTN" }));
        });
    }

    private static RoomComparisonStore CreateStore(params Guid[] ids)
    {
        var store = new RoomComparisonStore();
        foreach (var id in ids)
        {
            store.TryAdd(new ComparisonCandidate(id, $"Phòng {id:N}".Substring(0, 12), null, 0, 0));
        }

        return store;
    }

    private static ComparisonViewModel CreateViewModel(
        RoomComparisonStore store,
        FakeApiClient apiClient,
        FakeTravelEstimatesClient travelClient,
        string? campus,
        FakeErrorHandler? errorHandler = null)
        => new(
            store,
            apiClient,
            travelClient,
            new FakeCampusSelectionStore { SelectedCampus = campus },
            errorHandler ?? new FakeErrorHandler(),
            new FakeNavigationService());

    private static async Task WaitForIdleAsync(ComparisonViewModel viewModel)
    {
        for (var attempt = 0; attempt < 50 && viewModel.IsLoading; attempt++)
        {
            await Task.Delay(10);
        }
    }

    private static ListingDetailDto CreateDetail(
        Guid id,
        long price,
        long deposit,
        double area,
        params AmenityCode[] amenities)
        => new(
            id,
            $"Phòng {id:N}".Substring(0, 12),
            "Mô tả",
            price,
            deposit,
            area,
            RoomType.Studio,
            1,
            "1 Main St",
            10.77,
            106.66,
            ListingStatus.Published,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            amenities,
            Array.Empty<string>(),
            null,
            "Chủ trọ");

    private static TravelEstimatesFetchResult Found(TravelEstimateDto estimate)
        => new(TravelEstimatesFetchOutcome.Found, new ListingTravelEstimatesDto(new[] { estimate }));

    private static TravelEstimateDto Motorbike(int distanceM)
        => new(TravelMode.Motorbike, TravelSource.Route, distanceM, 60);

    private sealed class FakeApiClient : IApiClient
    {
        private readonly Dictionary<Guid, ListingDetailDto> _details = new();

        public FakeApiClient(params ListingDetailDto[] details)
        {
            foreach (var detail in details)
            {
                _details[detail.Id] = detail;
            }
        }

        public HashSet<Guid> Missing { get; } = new();

        public Exception? Exception { get; init; }

        public Task<ListingDetailFetchResult> GetListingDetailAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (Exception is { } exception)
            {
                return Task.FromException<ListingDetailFetchResult>(exception);
            }

            return Task.FromResult(_details.TryGetValue(id, out var detail) && !Missing.Contains(id)
                ? new ListingDetailFetchResult(ListingDetailFetchOutcome.Found, detail)
                : new ListingDetailFetchResult(ListingDetailFetchOutcome.NotFound, null));
        }

        public Task<ListingSearchResultDto> GetListingsAsync(
            int limit,
            string? cursor = null,
            ActiveListingFiltersDto? filters = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The comparison screen does not list rooms.");

        public Task<IReadOnlyList<ListingSummaryDto>> GetSimilarListingsAsync(
            Guid id,
            int limit = 4,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The comparison screen does not load similar listings.");

        public Task<ListingContactFetchResult> GetListingContactAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The comparison screen does not load contacts.");

        public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The comparison screen does not sign in.");

        public Task<LoginResponse?> DemoLoginAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The comparison screen does not sign in.");

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The comparison screen does not sign out.");

        public Task<SearchResultDto<ListingSummaryDto>> SearchByAreaAsync(
            AreaSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The comparison screen does not search by area.");

        public Task<SavedSearchDto> CreateSavedSearchAsync(
            CreateSavedSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The comparison screen does not create saved searches.");

        public Task<SavedSearchDto?> SetSavedSearchAlertAsync(
            Guid id,
            SavedSearchAlertState alertState,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The comparison screen does not change alerts.");

        public Task DeleteSavedSearchAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The comparison screen does not delete saved searches.");

        public Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
            int limit = 20,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The comparison screen does not load notifications.");
    }

    private sealed class FakeTravelEstimatesClient : ITravelEstimatesClient
    {
        public Dictionary<Guid, TravelEstimatesFetchResult> Estimates { get; } = new();

        public List<Guid> RequestedListingIds { get; } = new();

        public Task<TravelEstimatesFetchResult> GetEstimatesAsync(
            Guid listingId,
            string campusId,
            CancellationToken cancellationToken = default)
        {
            RequestedListingIds.Add(listingId);
            return Task.FromResult(Estimates.TryGetValue(listingId, out var result)
                ? result
                : new TravelEstimatesFetchResult(TravelEstimatesFetchOutcome.NotFound, null));
        }
    }

    private sealed class FakeCampusSelectionStore : ICampusSelectionStore
    {
        public string? SelectedCampus { get; set; }

        public string? GetSelectedCampus() => SelectedCampus;

        public void SetSelectedCampus(string? campusName) => SelectedCampus = campusName;
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public Exception? HandledException { get; private set; }

        public void HandleError(Exception ex) => HandledException = ex;
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public Task GoToRoomDetailAsync(Guid listingId) => Task.CompletedTask;

        public Task GoToLoginAsync() => Task.CompletedTask;

        public Task GoToDirectionsAsync(double destinationLat, double destinationLng, double? originLat, double? originLng)
            => Task.CompletedTask;

        public Task GoToComparisonAsync() => Task.CompletedTask;

        public Task GoBackAsync() => Task.CompletedTask;
    }
}
