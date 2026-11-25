using System.Collections.Generic;
using System.Threading.Tasks;
using RoomToRentRadar.Models;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Services;

namespace RoomToRentRadarUnitTests;

[NUnit.Framework.TestFixture]
public class PhotoGalleryViewModelTests
{
    private sealed class FakeNavigationService : INavigationService
    {
        public string? LastRoute { get; private set; }
        public IDictionary<string, object>? LastParameters { get; private set; }
        public int GoBackCount { get; private set; }

        public Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
        {
            LastRoute = route;
            LastParameters = parameters is null
                ? null
                : new Dictionary<string, object>(parameters);
            return Task.CompletedTask;
        }

        public Task GoBackAsync()
        {
            GoBackCount++;
            return Task.CompletedTask;
        }
    }

    [NUnit.Framework.Test]
    public async Task LoadPhotosAsync_WithNormalRoom_LoadsAtLeastEightPhotosAndDetects360()
    {
        var nav = new FakeNavigationService();
        var vm = new PhotoGalleryViewModel(new MockPhotoGalleryService(), nav);

        await vm.LoadPhotosAsync("room-2026-001");

        NUnit.Framework.Assert.That(vm.Photos.Count, NUnit.Framework.Is.GreaterThanOrEqualTo(8));
        NUnit.Framework.Assert.That(vm.HasPhotos, NUnit.Framework.Is.True);
        NUnit.Framework.Assert.That(vm.ShowPlaceholder, NUnit.Framework.Is.False);
        NUnit.Framework.Assert.That(vm.IsCurrentPhoto360, NUnit.Framework.Is.True);
        NUnit.Framework.Assert.That(vm.CounterText, NUnit.Framework.Is.EqualTo($"1 / {vm.Photos.Count}"));
    }

    [NUnit.Framework.Test]
    public async Task CurrentPositionChanged_ToStandardPhoto_Hides360Button()
    {
        var vm = new PhotoGalleryViewModel(new MockPhotoGalleryService(), new FakeNavigationService());
        await vm.LoadPhotosAsync("room-2026-001");

        vm.CurrentPosition = 1;

        NUnit.Framework.Assert.That(vm.IsCurrentPhoto360, NUnit.Framework.Is.False);
        NUnit.Framework.Assert.That(vm.CounterText, NUnit.Framework.Is.EqualTo($"2 / {vm.Photos.Count}"));
    }

    [NUnit.Framework.Test]
    public async Task LoadPhotosAsync_WithEmptyRoom_ShowsPlaceholderImage()
    {
        var vm = new PhotoGalleryViewModel(new MockPhotoGalleryService(), new FakeNavigationService());

        await vm.LoadPhotosAsync("room-empty");

        NUnit.Framework.Assert.That(vm.Photos.Count, NUnit.Framework.Is.EqualTo(0));
        NUnit.Framework.Assert.That(vm.HasPhotos, NUnit.Framework.Is.False);
        NUnit.Framework.Assert.That(vm.ShowPlaceholder, NUnit.Framework.Is.True);
        NUnit.Framework.Assert.That(vm.CounterText, NUnit.Framework.Is.EqualTo("0 / 0"));
    }

    [NUnit.Framework.Test]
    public async Task Open360ViewerCommand_On360Photo_NavigatesToViewer360Route()
    {
        var nav = new FakeNavigationService();
        var vm = new PhotoGalleryViewModel(new MockPhotoGalleryService(), nav);
        await vm.LoadPhotosAsync("room-2026-001");

        await vm.Open360ViewerCommand.ExecuteAsync(null);

        NUnit.Framework.Assert.That(nav.LastRoute, NUnit.Framework.Is.EqualTo("viewer-360"));
        NUnit.Framework.Assert.That(nav.LastParameters, NUnit.Framework.Is.Not.Null);
        NUnit.Framework.Assert.That(nav.LastParameters!.ContainsKey("imageUrl"), NUnit.Framework.Is.True);
        NUnit.Framework.Assert.That(nav.LastParameters.ContainsKey("caption"), NUnit.Framework.Is.True);
    }

    [NUnit.Framework.Test]
    public async Task RoomListViewModel_OpenRoomDetailCommand_NavigatesToRoomDetailViewWithId()
    {
        var nav = new FakeNavigationService();
        var vm = new RoomListViewModel(new MockListingsService(), nav);
        var room = new RoomListingItemModel
        {
            Id = "room-ictu-001",
            Title = "Phòng trọ test ICTU"
        };

        await vm.OpenRoomDetailCommand.ExecuteAsync(room);

        NUnit.Framework.Assert.That(nav.LastRoute, NUnit.Framework.Is.EqualTo("room-detail-view"));
        NUnit.Framework.Assert.That(nav.LastParameters, NUnit.Framework.Is.Not.Null);
        NUnit.Framework.Assert.That(nav.LastParameters!["id"], NUnit.Framework.Is.EqualTo("room-ictu-001"));
    }

    [NUnit.Framework.Test]
    public async Task MapSearchViewModel_OpenRoomDetailCommand_NavigatesToRoomDetailViewWithId()
    {
        var nav = new FakeNavigationService();
        var vm = new MapSearchViewModel(new MockSpatialSearchService(), nav);
        var pin = new SpatialSearchPinDto
        {
            Id = "room-map-001",
            Title = "Phòng quanh ICTU"
        };

        await vm.OpenRoomDetailCommand.ExecuteAsync(pin);

        NUnit.Framework.Assert.That(nav.LastRoute, NUnit.Framework.Is.EqualTo("room-detail-view"));
        NUnit.Framework.Assert.That(nav.LastParameters, NUnit.Framework.Is.Not.Null);
        NUnit.Framework.Assert.That(nav.LastParameters!["id"], NUnit.Framework.Is.EqualTo("room-map-001"));
    }
}
