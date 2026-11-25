using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;
using RoomToRentRadar.ViewModels;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Shared.Favourites;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class FavouritesViewModelTests
{
    private static readonly Guid FirstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Test]
    public async Task LoadAsync_WhenSignedOut_ShowsSignInPrompt()
    {
        var api = new FakeFavouritesApiClient { SignedIn = false };
        var viewModel = CreateViewModel(new FavouritesService(api));

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(FavouritesState.SignedOut));
            Assert.That(viewModel.IsSignedOutVisible, Is.True);
            Assert.That(viewModel.IsContentVisible, Is.False);
            Assert.That(viewModel.Favourites, Is.Empty);
        });
    }

    [Test]
    public async Task LoadAsync_WithFavourites_ShowsCurrentPriceAndAvailability()
    {
        var api = new FakeFavouritesApiClient();
        api.Items.Add(CreateFavourite(FirstId, "Phòng gần trường", 6_500_000, ListingStatus.Published));
        api.Items.Add(CreateFavourite(SecondId, "Phòng đã cho thuê", 4_000_000, ListingStatus.Hidden));
        var viewModel = CreateViewModel(new FavouritesService(api));

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(FavouritesState.Content));
            Assert.That(viewModel.IsContentVisible, Is.True);
            Assert.That(viewModel.Favourites, Has.Count.EqualTo(2));
            var first = viewModel.Favourites[0];
            Assert.That(first.Title, Is.EqualTo("Phòng gần trường"));
            Assert.That(first.PriceText, Is.EqualTo("6.500.000 đ"));
            Assert.That(first.StatusText, Is.EqualTo("Còn trống"));
            Assert.That(first.IsAvailable, Is.True);
            var second = viewModel.Favourites[1];
            Assert.That(second.PriceText, Is.EqualTo("4.000.000 đ"));
            Assert.That(second.StatusText, Is.EqualTo("Tạm ẩn"));
            Assert.That(second.IsAvailable, Is.False);
        });
    }

    [Test]
    public async Task LoadAsync_WithoutFavourites_ShowsEmptyState()
    {
        var api = new FakeFavouritesApiClient();
        var viewModel = CreateViewModel(new FavouritesService(api));

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(FavouritesState.Empty));
            Assert.That(viewModel.IsEmptyVisible, Is.True);
            Assert.That(viewModel.Favourites, Is.Empty);
        });
    }

    [Test]
    public async Task LoadAsync_WhenApiThrows_ShowsErrorAndReportsException()
    {
        var exception = new HttpRequestException("network down");
        var api = new FakeFavouritesApiClient { GetException = exception };
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(new FavouritesService(api), errorHandler: errorHandler);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(FavouritesState.Error));
            Assert.That(viewModel.IsErrorVisible, Is.True);
            Assert.That(errorHandler.HandledExceptions, Has.Count.EqualTo(1));
            Assert.That(errorHandler.HandledExceptions[0], Is.SameAs(exception));
        });
    }

    [Test]
    public async Task LoadAsync_WhenRefreshFailsAfterContent_KeepsExistingList()
    {
        var api = new FakeFavouritesApiClient();
        api.Items.Add(CreateFavourite(FirstId, "Phòng gần trường", 6_500_000, ListingStatus.Published));
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(new FavouritesService(api), errorHandler: errorHandler);
        await viewModel.LoadCommand.ExecuteAsync(null);

        api.GetException = new HttpRequestException("network down");
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(FavouritesState.Content));
            Assert.That(viewModel.Favourites, Has.Count.EqualTo(1));
            Assert.That(errorHandler.HandledExceptions, Is.Empty);
        });
    }

    [Test]
    public async Task RemoveAsync_RemovesItemAndOffersUndo()
    {
        var api = new FakeFavouritesApiClient();
        api.Items.Add(CreateFavourite(FirstId, "Phòng gần trường", 6_500_000, ListingStatus.Published));
        var undoService = new FakeUndoService();
        var viewModel = CreateViewModel(new FavouritesService(api), undoService);
        await viewModel.LoadCommand.ExecuteAsync(null);
        var item = viewModel.Favourites[0];

        await viewModel.RemoveCommand.ExecuteAsync(item);

        Assert.Multiple(() =>
        {
            Assert.That(api.RemovedIds, Is.EqualTo(new[] { FirstId }));
            Assert.That(viewModel.Favourites, Is.Empty);
            Assert.That(viewModel.State, Is.EqualTo(FavouritesState.Empty));
            Assert.That(undoService.Offered, Has.Count.EqualTo(1));
            Assert.That(undoService.Offered[0].ListingId, Is.EqualTo(FirstId));
        });
    }

    [Test]
    public async Task RemoveAsync_WhenUndoIsTaken_RestoresItem()
    {
        var api = new FakeFavouritesApiClient();
        api.Items.Add(CreateFavourite(FirstId, "Phòng gần trường", 6_500_000, ListingStatus.Published));
        var favouritesService = new FavouritesService(api);
        var undoService = new FakeUndoService
        {
            OnOffer = item => favouritesService.RestoreAsync(item)
        };
        var viewModel = CreateViewModel(favouritesService, undoService);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.RemoveCommand.ExecuteAsync(viewModel.Favourites[0]);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(FavouritesState.Content));
            Assert.That(viewModel.Favourites, Has.Count.EqualTo(1));
            Assert.That(viewModel.Favourites[0].ListingId, Is.EqualTo(FirstId));
            Assert.That(viewModel.Favourites[0].Title, Is.EqualTo("Phòng gần trường"));
            Assert.That(api.AddedIds, Is.EqualTo(new[] { FirstId }));
        });
    }

    [Test]
    public async Task RemoveAsync_WhenApiThrows_KeepsItemAndReportsException()
    {
        var api = new FakeFavouritesApiClient();
        api.Items.Add(CreateFavourite(FirstId, "Phòng gần trường", 6_500_000, ListingStatus.Published));
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(new FavouritesService(api), errorHandler: errorHandler);
        await viewModel.LoadCommand.ExecuteAsync(null);

        api.RemoveException = new HttpRequestException("network down");
        await viewModel.RemoveCommand.ExecuteAsync(viewModel.Favourites[0]);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Favourites, Has.Count.EqualTo(1));
            Assert.That(viewModel.State, Is.EqualTo(FavouritesState.Content));
            Assert.That(errorHandler.HandledExceptions, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task OpenRoomCommand_NavigatesToRoomDetail()
    {
        var api = new FakeFavouritesApiClient();
        api.Items.Add(CreateFavourite(FirstId, "Phòng gần trường", 6_500_000, ListingStatus.Published));
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(new FavouritesService(api), navigation: navigationService);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.OpenRoomCommand.ExecuteAsync(viewModel.Favourites[0]);

        Assert.That(navigationService.RequestedListingIds, Is.EqualTo(new[] { FirstId }));
    }

    [Test]
    public async Task ShowLoginCommand_WhenSignedOut_NavigatesToLogin()
    {
        var api = new FakeFavouritesApiClient { SignedIn = false };
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(new FavouritesService(api), navigation: navigationService);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.ShowLoginCommand.ExecuteAsync(null);

        Assert.That(navigationService.GoToLoginCount, Is.EqualTo(1));
    }

    private static FavouritesViewModel CreateViewModel(
        FavouritesService favouritesService,
        FakeUndoService? undoService = null,
        FakeErrorHandler? errorHandler = null,
        FakeNavigationService? navigation = null)
        => new(
            favouritesService,
            undoService ?? new FakeUndoService(),
            errorHandler ?? new FakeErrorHandler(),
            navigation ?? new FakeNavigationService());

    private static FavouriteRoomDto CreateFavourite(
        Guid listingId,
        string title,
        long price,
        ListingStatus status)
        => new(
            listingId,
            title,
            price,
            32,
            1,
            "12 Nguyễn Trãi, Quận 5",
            status,
            null,
            new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc));

    private sealed class FakeFavouritesApiClient : IFavouritesApiClient
    {
        public bool SignedIn { get; set; } = true;

        public List<FavouriteRoomDto> Items { get; } = new();

        public List<Guid> AddedIds { get; } = new();

        public List<Guid> RemovedIds { get; } = new();

        public Exception? GetException { get; set; }

        public Exception? RemoveException { get; set; }

        public Task<FavouritesFetchResult> GetFavouritesAsync(CancellationToken cancellationToken = default)
            => GetException is { } exception
                ? Task.FromException<FavouritesFetchResult>(exception)
                : Task.FromResult(new FavouritesFetchResult(
                    SignedIn ? FavouritesFetchOutcome.Found : FavouritesFetchOutcome.Unauthorized,
                    SignedIn ? Items.ToList() : []));

        public Task AddFavouriteAsync(Guid listingId, CancellationToken cancellationToken = default)
        {
            AddedIds.Add(listingId);
            if (!Items.Exists(item => item.ListingId == listingId))
            {
                Items.Add(CreateFavourite(
                    listingId,
                    "Khôi phục",
                    5_000_000,
                    ListingStatus.Published));
            }

            return Task.CompletedTask;
        }

        public Task RemoveFavouriteAsync(Guid listingId, CancellationToken cancellationToken = default)
        {
            if (RemoveException is { } exception)
                return Task.FromException(exception);

            RemovedIds.Add(listingId);
            Items.RemoveAll(item => item.ListingId == listingId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUndoService : IUndoService
    {
        public List<FavouriteRoomDto> Offered { get; } = new();

        public Func<FavouriteRoomDto, Task>? OnOffer { get; set; }

        public async Task OfferRemovalUndoAsync(
            FavouriteRoomDto item,
            CancellationToken cancellationToken = default)
        {
            Offered.Add(item);

            if (OnOffer is not null)
                await OnOffer(item);
        }
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public List<Exception> HandledExceptions { get; } = new();

        public void HandleError(Exception ex)
        {
            HandledExceptions.Add(ex);
        }
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public Task GoToListingEditorAsync(Guid? listingId = null) => Task.CompletedTask;

        public List<Guid> RequestedListingIds { get; } = new();

        public int GoToLoginCount { get; private set; }

        public Task GoToRoomDetailAsync(Guid listingId)
        {
            RequestedListingIds.Add(listingId);
            return Task.CompletedTask;
        }

        public Task GoToLoginAsync()
        {
            GoToLoginCount++;
            return Task.CompletedTask;
        }

        public Task GoToRegisterAsync() => Task.CompletedTask;

        public Task GoToRoleRootAsync(RoomToRentRadar.Models.AppUserRole? role) => Task.CompletedTask;

        public Task GoToDirectionsAsync(
            double destinationLat,
            double destinationLng,
            double? originLat,
            double? originLng)
            => throw new NotSupportedException("The favourites screen does not open directions.");

        public Task GoToComparisonAsync()
            => throw new NotSupportedException("The favourites screen does not open room comparison.");

        public Task GoBackAsync() => throw new NotSupportedException("The favourites screen does not go back directly.");
    }
}
