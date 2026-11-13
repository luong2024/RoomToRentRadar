using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.RoomListings.Models;
using RoomToRentRadar.Features.RoomListings.Services;
using RoomToRentRadar.Features.RoomListings.ViewModels;

namespace RoomToRentRadarUnitTests.Features.RoomListings;

[TestFixture]
public class RoomDetailsViewModelTests
{
    [Test]
    public async Task LoadRoomById_WhenRoomExists_PopulatesRoomAndUpdatesTitle()
    {
        // Arrange
        var expectedRoom = new RoomListItemModel
        {
            Id = "room-ictu-001",
            Title = "Phòng trọ sát cổng chính ICTU",
            Price = 1_800_000m,
            AreaSqm = 22,
            Address = "Ngõ 12 Đường Z115"
        };
        var service = new StubRoomListingsService(_ => Task.FromResult<RoomListItemModel?>(expectedRoom));
        var errorHandler = new StubErrorHandler();
        var vm = new RoomDetailsViewModel(service, errorHandler);

        // Act
        await vm.LoadRoomByIdCommand.ExecuteAsync("room-ictu-001");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.HasRoom, Is.True);
            Assert.That(vm.Room, Is.SameAs(expectedRoom));
            Assert.That(vm.Title, Is.EqualTo("Phòng trọ sát cổng chính ICTU"));
            Assert.That(vm.IsNotFound, Is.False);
            Assert.That(vm.IsLoading, Is.False);
        });
    }

    [Test]
    public async Task LoadRoomById_WhenRoomMissing_SetsIsNotFoundTrue()
    {
        // Arrange
        var service = new StubRoomListingsService(_ => Task.FromResult<RoomListItemModel?>(null));
        var errorHandler = new StubErrorHandler();
        var vm = new RoomDetailsViewModel(service, errorHandler);

        // Act
        await vm.LoadRoomByIdCommand.ExecuteAsync("missing-room");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.HasRoom, Is.False);
            Assert.That(vm.IsNotFound, Is.True);
            Assert.That(vm.StatusMessage, Is.Not.Empty);
        });
    }

    [Test]
    public async Task GoBack_WhenExecuted_NavigatesUpOneLevel()
    {
        // Arrange
        var navigated = new List<string>();
        var service = new StubRoomListingsService(_ => Task.FromResult<RoomListItemModel?>(null));
        var errorHandler = new StubErrorHandler();
        var vm = new RoomDetailsViewModel(service, errorHandler, route =>
        {
            navigated.Add(route);
            return Task.CompletedTask;
        });

        // Act
        await vm.GoBackCommand.ExecuteAsync(null);

        // Assert
        Assert.That(navigated, Is.EqualTo(new[] { ".." }));
    }

    private sealed class StubRoomListingsService(Func<string, Task<RoomListItemModel?>> byIdHandler) : IRoomListingsService
    {
        public Task<PagedResponse<RoomListItemModel>> GetRoomListingsAsync(
            int page,
            int pageSize,
            RoomSortOption sortBy = RoomSortOption.Newest,
            string? search = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResponse<RoomListItemModel>([], page, pageSize, 0));

        public Task<RoomListItemModel?> GetRoomByIdAsync(string roomId, CancellationToken cancellationToken = default) =>
            byIdHandler(roomId);
    }

    private sealed class StubErrorHandler : IErrorHandler
    {
        public List<Exception> HandledExceptions { get; } = [];

        public void HandleError(Exception ex) => HandledExceptions.Add(ex);
    }
}
