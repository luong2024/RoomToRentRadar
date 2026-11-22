using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;
using RoomToRentRadar.Shared.Favourites;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class UndoServiceTests
{
    [Test]
    public async Task OfferRemovalUndoAsync_ShowsFiveSecondPromptWithUndoAction()
    {
        var prompt = new FakeUndoPrompt();
        var favouritesService = new FakeFavouritesService();
        var undoService = new UndoService(favouritesService, prompt);
        var item = CreateFavourite();

        await undoService.OfferRemovalUndoAsync(item);

        Assert.Multiple(() =>
        {
            Assert.That(prompt.Message, Does.Contain(item.Title));
            Assert.That(prompt.ActionText, Is.EqualTo("Hoàn tác"));
            Assert.That(prompt.Duration, Is.EqualTo(TimeSpan.FromSeconds(5)));
        });
    }

    [Test]
    public async Task OfferRemovalUndoAsync_WhenActionTapped_RestoresFavourite()
    {
        var prompt = new FakeUndoPrompt { Result = true };
        var favouritesService = new FakeFavouritesService();
        var undoService = new UndoService(favouritesService, prompt);
        var item = CreateFavourite();

        await undoService.OfferRemovalUndoAsync(item);

        Assert.That(favouritesService.Restored, Is.EqualTo(new[] { item }));
    }

    [Test]
    public async Task OfferRemovalUndoAsync_WhenActionNotTapped_DoesNotRestore()
    {
        var prompt = new FakeUndoPrompt { Result = false };
        var favouritesService = new FakeFavouritesService();
        var undoService = new UndoService(favouritesService, prompt);

        await undoService.OfferRemovalUndoAsync(CreateFavourite());

        Assert.That(favouritesService.Restored, Is.Empty);
    }

    private static FavouriteRoomDto CreateFavourite()
        => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Phòng gần trường",
            6_500_000,
            32,
            1,
            "12 Nguyễn Trãi, Quận 5",
            ListingStatus.Published,
            null,
            new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc));

    private sealed class FakeUndoPrompt : IUndoPrompt
    {
        public string? Message { get; private set; }

        public string? ActionText { get; private set; }

        public TimeSpan Duration { get; private set; }

        public bool Result { get; set; }

        public Task<bool> ShowAsync(
            string message,
            string actionText,
            TimeSpan duration,
            CancellationToken cancellationToken = default)
        {
            Message = message;
            ActionText = actionText;
            Duration = duration;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeFavouritesService : IFavouritesService
    {
        public List<FavouriteRoomDto> Restored { get; } = new();

        public IReadOnlyList<FavouriteRoomDto> Items => [];

        public bool IsFavourite(Guid listingId) => false;

        public Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task AddAsync(Guid listingId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<FavouriteRoomDto?> RemoveAsync(
            Guid listingId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<FavouriteRoomDto?>(null);

        public Task RestoreAsync(FavouriteRoomDto item, CancellationToken cancellationToken = default)
        {
            Restored.Add(item);
            return Task.CompletedTask;
        }
    }
}
