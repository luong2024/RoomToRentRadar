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
            Assert.That(viewModel.ResultsText, Is.EqualTo("Đã tải 2 phòng"));
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.IsRefreshing, Is.False);
            Assert.That(viewModel.HasError, Is.False);
            Assert.That(viewModel.CanLoadMore, Is.True);
            Assert.That(apiClient.RequestedLimits, Is.EqualTo(new[] { 12 }));
            Assert.That(apiClient.RequestedCursors, Is.EqualTo(new string?[] { null }));
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
            CreatePage("cursor-2", first),
            CreatePage(null, second));
        var viewModel = new HomeViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.LoadMoreCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Listings, Has.Count.EqualTo(2));
            Assert.That(viewModel.Listings[1].Id, Is.EqualTo(second.Id));
            Assert.That(viewModel.ResultsText, Is.EqualTo("Đã tải 2 phòng"));
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
            Assert.That(viewModel.ResultsText, Is.EqualTo("Đã tải 2 phòng"));
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
            Assert.That(viewModel.ResultsText, Is.EqualTo("Đã tải 1 phòng"));
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

    private static PagedResult<ListingSummaryDto> CreatePage(
        string? nextCursor,
        params ListingSummaryDto[] items)
        => new(items, new PaginationInfo(12, nextCursor, nextCursor is not null));

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

        public Task<PagedResult<ListingSummaryDto>> GetListingsAsync(
            int limit,
            string? cursor = null,
            CancellationToken cancellationToken = default)
        {
            RequestedLimits.Add(limit);
            RequestedCursors.Add(cursor);

            if (_responses.Count == 0)
                throw new InvalidOperationException("No fake listings response was queued.");

            return _responses.Dequeue() switch
            {
                PagedResult<ListingSummaryDto> page => Task.FromResult(page),
                Exception exception => Task.FromException<PagedResult<ListingSummaryDto>>(exception),
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
