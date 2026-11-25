using System;
using System.Threading.Tasks;
using RoomToRentRadar.Models;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Services;

namespace RoomToRentRadarUnitTests;

[NUnit.Framework.TestFixture]
public class FavouritesViewModelTests
{
    [NUnit.Framework.Test]
    public async Task LoadFavouritesAsync_LoadsSavedRoomsWithHeartActive()
    {
        var vm = new FavouritesViewModel(new MockFavouritesService(), TimeSpan.FromMilliseconds(50));

        await vm.LoadFavouritesAsync();

        NUnit.Framework.Assert.That(vm.Favourites.Count, NUnit.Framework.Is.EqualTo(2));
        NUnit.Framework.Assert.That(vm.IsEmpty, NUnit.Framework.Is.False);
        NUnit.Framework.Assert.That(vm.Favourites[0].IsFavourite, NUnit.Framework.Is.True);
    }

    [NUnit.Framework.Test]
    public async Task RemoveWithUndoAsync_WhenUndoClickedWithinWindow_RestoresRoomAndDoesNotDelete()
    {
        var service = new MockFavouritesService();
        var vm = new FavouritesViewModel(service, TimeSpan.FromMilliseconds(200));
        await vm.LoadFavouritesAsync();

        var firstRoom = vm.Favourites[0];
        var removeTask = vm.RemoveWithUndoCommand.ExecuteAsync(firstRoom);

        NUnit.Framework.Assert.That(vm.ShowUndoBar, NUnit.Framework.Is.True);
        NUnit.Framework.Assert.That(vm.Favourites.Count, NUnit.Framework.Is.EqualTo(1));

        vm.UndoRemoveCommand.Execute(null);
        await removeTask;

        NUnit.Framework.Assert.That(vm.ShowUndoBar, NUnit.Framework.Is.False);
        NUnit.Framework.Assert.That(vm.Favourites.Count, NUnit.Framework.Is.EqualTo(2));
        NUnit.Framework.Assert.That(vm.Favourites[0].Id, NUnit.Framework.Is.EqualTo(firstRoom.Id));
        NUnit.Framework.Assert.That(await service.IsFavouriteAsync(firstRoom.Id), NUnit.Framework.Is.True);
    }

    [NUnit.Framework.Test]
    public async Task RemoveWithUndoAsync_WhenTimerExpires_CommitsDeletionInService()
    {
        var service = new MockFavouritesService();
        var vm = new FavouritesViewModel(service, TimeSpan.FromMilliseconds(50));
        await vm.LoadFavouritesAsync();

        var firstRoom = vm.Favourites[0];
        await vm.RemoveWithUndoCommand.ExecuteAsync(firstRoom);

        NUnit.Framework.Assert.That(vm.ShowUndoBar, NUnit.Framework.Is.False);
        NUnit.Framework.Assert.That(vm.Favourites.Count, NUnit.Framework.Is.EqualTo(1));
        NUnit.Framework.Assert.That(await service.IsFavouriteAsync(firstRoom.Id), NUnit.Framework.Is.False);
    }

    [NUnit.Framework.Test]
    public async Task RoomListViewModel_ToggleFavouriteCommand_UpdatesHeartStateAndSyncsService()
    {
        var favService = new MockFavouritesService();
        var listVm = new RoomListViewModel(new MockListingsService(), favService);
        var room = new RoomListingItemModel
        {
            Id = "room-ictu-099",
            Title = "Phòng mới test yêu thích"
        };

        await listVm.ToggleFavouriteCommand.ExecuteAsync(room);
        NUnit.Framework.Assert.That(room.IsFavourite, NUnit.Framework.Is.True);
        NUnit.Framework.Assert.That(await favService.IsFavouriteAsync("room-ictu-099"), NUnit.Framework.Is.True);

        await listVm.ToggleFavouriteCommand.ExecuteAsync(room);
        NUnit.Framework.Assert.That(room.IsFavourite, NUnit.Framework.Is.False);
        NUnit.Framework.Assert.That(await favService.IsFavouriteAsync("room-ictu-099"), NUnit.Framework.Is.False);
    }
}
