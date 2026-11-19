using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.Favourites.ViewModels;
using RoomToRentRadar.Infrastructure.Favourites;
using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class FavouritesViewModelTests
{
    [Test]
    public void DefaultUndoWindow_IsFiveSeconds()
        => Assert.That(FavouritesViewModel.DefaultUndoWindow, Is.EqualTo(TimeSpan.FromSeconds(5)));

    [Test]
    public async Task LoadAsync_RendersCurrentPriceAndAvailability()
    {
        var available = CreateFavourite("Available room", isAvailable: true, price: 2_500_000);
        var unavailable = CreateFavourite("Unavailable room", isAvailable: false);
        var store = new TestFavouritesStore { NextRefresh = [available, unavailable] };
        var viewModel = CreateViewModel(store);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(store.RefreshCount, Is.EqualTo(1));
            Assert.That(viewModel.Items, Has.Count.EqualTo(2));
            Assert.That(viewModel.Items[0].Title, Is.EqualTo("Available room"));
            Assert.That(viewModel.Items[0].PriceText, Is.EqualTo("2.500.000 đ"));
            Assert.That(viewModel.Items[0].AvailabilityText, Is.EqualTo("● Còn trống"));
            Assert.That(viewModel.Items[0].IsAvailable, Is.True);
            Assert.That(viewModel.Items[1].AvailabilityText, Is.EqualTo("● Không còn khả dụng"));
            Assert.That(viewModel.Items[1].IsAvailable, Is.False);
            Assert.That(viewModel.HasError, Is.False);
            Assert.That(viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task LoadAsync_WhenStoreFails_ShowsErrorAndReportsException()
    {
        var exception = new HttpRequestException("offline");
        var store = new TestFavouritesStore { LoadException = exception };
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(store, errorHandler);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Items, Is.Empty);
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Không thể tải danh sách yêu thích. Vui lòng thử lại."));
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(errorHandler.HandledExceptions, Has.Count.EqualTo(1));
            Assert.That(errorHandler.HandledExceptions[0], Is.SameAs(exception));
        });
    }

    [Test]
    public async Task RemoveAsync_RemovesItemAndOffersUndo()
    {
        var first = CreateFavourite("First room");
        var second = CreateFavourite("Second room");
        var store = new TestFavouritesStore(first, second);
        var viewModel = CreateViewModel(store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.RemoveCommand.ExecuteAsync(viewModel.Items[0]);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Items.Select(item => item.ListingId), Is.EqualTo(new[] { second.ListingId }));
            Assert.That(store.Removed, Is.EqualTo(new[] { first.ListingId }));
            Assert.That(viewModel.IsUndoVisible, Is.True);
            Assert.That(viewModel.UndoMessage, Is.EqualTo("Đã bỏ lưu \"First room\""));
        });
    }

    [Test]
    public async Task UndoAsync_WithinWindow_RestoresItemAtOriginalPosition()
    {
        var first = CreateFavourite("First room");
        var second = CreateFavourite("Second room");
        var store = new TestFavouritesStore(first, second);
        var viewModel = CreateViewModel(store);
        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.RemoveCommand.ExecuteAsync(viewModel.Items[0]);

        await viewModel.UndoCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Items.Select(item => item.ListingId),
                Is.EqualTo(new[] { first.ListingId, second.ListingId }));
            Assert.That(store.Added, Is.EqualTo(new[] { first.ListingId }));
            Assert.That(viewModel.IsUndoVisible, Is.False);
            Assert.That(viewModel.UndoMessage, Is.Empty);
        });
    }

    [Test]
    public async Task RemoveAsync_WhenUndoWindowExpires_HidesUndoAndKeepsRemoval()
    {
        var first = CreateFavourite("First room");
        var second = CreateFavourite("Second room");
        var store = new TestFavouritesStore(first, second);
        var viewModel = CreateViewModel(store, undoWindow: TimeSpan.FromMilliseconds(40));
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.RemoveCommand.ExecuteAsync(viewModel.Items[0]);
        Assert.That(viewModel.IsUndoVisible, Is.True);

        await WaitUntilAsync(
            () => !viewModel.IsUndoVisible,
            "The undo banner should disappear after the undo window elapses.");

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Items.Select(item => item.ListingId), Is.EqualTo(new[] { second.ListingId }));
            Assert.That(store.Added, Is.Empty);
        });
    }

    [Test]
    public async Task RemoveAsync_WhenStoreFails_RestoresItemAndShowsError()
    {
        var first = CreateFavourite("First room");
        var second = CreateFavourite("Second room");
        var exception = new HttpRequestException("offline");
        var store = new TestFavouritesStore(first, second) { RemoveException = exception };
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(store, errorHandler);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.RemoveCommand.ExecuteAsync(viewModel.Items[0]);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Items.Select(item => item.ListingId),
                Is.EqualTo(new[] { first.ListingId, second.ListingId }));
            Assert.That(viewModel.IsUndoVisible, Is.False);
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Không thể bỏ lưu phòng này. Vui lòng thử lại."));
            Assert.That(errorHandler.HandledExceptions, Has.Count.EqualTo(1));
            Assert.That(errorHandler.HandledExceptions[0], Is.SameAs(exception));
        });
    }

    [Test]
    public async Task RemoveAsync_AfterEarlierFailure_ClearsTheStaleError()
    {
        var first = CreateFavourite("First room");
        var second = CreateFavourite("Second room");
        var store = new TestFavouritesStore(first, second) { RemoveException = new HttpRequestException("offline") };
        var viewModel = CreateViewModel(store);
        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.RemoveCommand.ExecuteAsync(viewModel.Items[0]);
        Assert.That(viewModel.HasError, Is.True);

        store.RemoveException = null;
        await viewModel.RemoveCommand.ExecuteAsync(viewModel.Items[0]);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasError, Is.False);
            Assert.That(viewModel.IsUndoVisible, Is.True);
            Assert.That(viewModel.Items.Select(item => item.ListingId), Is.EqualTo(new[] { second.ListingId }));
        });
    }

    [Test]
    public async Task UndoAsync_WhenStoreFails_RemovesItemAgainAndShowsError()
    {
        var first = CreateFavourite("First room");
        var second = CreateFavourite("Second room");
        var exception = new HttpRequestException("offline");
        var store = new TestFavouritesStore(first, second) { AddException = exception };
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(store, errorHandler);
        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.RemoveCommand.ExecuteAsync(viewModel.Items[0]);

        await viewModel.UndoCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Items.Select(item => item.ListingId), Is.EqualTo(new[] { second.ListingId }));
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Không thể hoàn tác. Phòng đã bị bỏ lưu."));
            Assert.That(errorHandler.HandledExceptions, Has.Count.EqualTo(1));
            Assert.That(errorHandler.HandledExceptions[0], Is.SameAs(exception));
        });
    }

    [Test]
    public async Task CancelUndoWindow_HidesUndoAndPreventsRestore()
    {
        var first = CreateFavourite("First room");
        var second = CreateFavourite("Second room");
        var store = new TestFavouritesStore(first, second);
        var viewModel = CreateViewModel(store);
        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.RemoveCommand.ExecuteAsync(viewModel.Items[0]);

        viewModel.CancelUndoWindow();
        await viewModel.UndoCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsUndoVisible, Is.False);
            Assert.That(viewModel.Items.Select(item => item.ListingId), Is.EqualTo(new[] { second.ListingId }));
            Assert.That(store.Added, Is.Empty);
        });
    }

    [Test]
    public async Task OpenAsync_WhenItemSelected_NavigatesToRoomDetail()
    {
        var favourite = CreateFavourite("First room");
        var store = new TestFavouritesStore(favourite);
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(store, navigationService: navigationService);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.OpenCommand.ExecuteAsync(viewModel.Items[0]);

        Assert.That(navigationService.RequestedListingIds, Is.EqualTo(new[] { favourite.ListingId }));
    }

    private static FavouritesViewModel CreateViewModel(
        IFavouritesStore store,
        FakeErrorHandler? errorHandler = null,
        FakeNavigationService? navigationService = null,
        TimeSpan? undoWindow = null)
        => new(
            store,
            errorHandler ?? new FakeErrorHandler(),
            navigationService ?? new FakeNavigationService(),
            undoWindow);

    private static FavouriteListingDto CreateFavourite(
        string title,
        bool isAvailable = true,
        long price = 1_500_000)
        => new(
            Guid.NewGuid(),
            title,
            price,
            25,
            1,
            "1 Main St",
            16.0,
            108.0,
            null,
            isAvailable,
            DateTime.UtcNow);

    private static async Task WaitUntilAsync(Func<bool> condition, string message)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail(message);

            await Task.Delay(10);
        }
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public List<Exception> HandledExceptions { get; } = [];

        public void HandleError(Exception ex) => HandledExceptions.Add(ex);
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public List<Guid> RequestedListingIds { get; } = [];

        public Task GoToRoomDetailAsync(Guid listingId)
        {
            RequestedListingIds.Add(listingId);
            return Task.CompletedTask;
        }

        public Task GoToLoginAsync() => Task.CompletedTask;

        public Task GoToDirectionsAsync(
            double destinationLat,
            double destinationLng,
            double? originLat,
            double? originLng)
            => throw new NotSupportedException("The favourites screen does not open directions.");

        public Task GoBackAsync() => Task.CompletedTask;
    }
}
