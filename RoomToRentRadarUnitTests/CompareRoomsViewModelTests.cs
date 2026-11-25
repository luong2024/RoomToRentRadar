using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RoomToRentRadar.Models;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Services;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class CompareRoomsViewModelTests
{
    private sealed class FailingListingsService : IListingsService
    {
        public Task<PagedResult<RoomListingItemModel>> GetListingsAsync(
            string campusId,
            int pageIndex,
            int pageSize,
            string sortBy,
            CancellationToken cancellationToken = default)
        {
            throw new HttpRequestException("API unreachable");
        }
    }

    [Test]
    public async Task LoadAvailableRoomsCommand_LoadsRoomsAndPreselectsTwoForComparison()
    {
        var vm = new CompareRoomsViewModel(new MockListingsService());

        await vm.LoadAvailableRoomsCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.HasError, Is.False);
            Assert.That(vm.AvailableRooms, Is.Not.Empty);
            Assert.That(vm.ComparedRooms, Has.Count.EqualTo(2));
            Assert.That(vm.HasSlot1, Is.True);
            Assert.That(vm.HasSlot2, Is.True);
            Assert.That(vm.HasSlot3, Is.False);
            Assert.That(vm.ComparisonRows, Has.Count.EqualTo(10));
            Assert.That(vm.CompareCountText, Is.EqualTo("Đang so sánh 2/3 phòng"));
            Assert.That(vm.HasBestValueSummary, Is.True);
        });
    }

    [Test]
    public async Task AddRoomCommand_AddsThirdRoomAndUpdatesAllThreeSlots()
    {
        var vm = new CompareRoomsViewModel(new MockListingsService());
        await vm.LoadAvailableRoomsCommand.ExecuteAsync(null);
        var thirdRoom = vm.AvailableRooms[2];

        vm.AddRoomCommand.Execute(thirdRoom);

        Assert.Multiple(() =>
        {
            Assert.That(vm.ComparedRooms, Has.Count.EqualTo(3));
            Assert.That(vm.HasSlot3, Is.True);
            Assert.That(vm.Slot3, Is.SameAs(thirdRoom));
            Assert.That(thirdRoom.IsInComparison, Is.True);
            Assert.That(vm.CanAddMoreRooms, Is.False);
            Assert.That(vm.CompareCountText, Is.EqualTo("Đang so sánh 3/3 phòng"));
            Assert.That(vm.ComparisonRows[0].HasSlot3, Is.True);
        });
    }

    [Test]
    public async Task AddRoomCommand_ExceedingThreeRooms_RejectsFourthRoom()
    {
        var vm = new CompareRoomsViewModel(new MockListingsService());
        await vm.LoadAvailableRoomsCommand.ExecuteAsync(null);
        vm.AddRoomCommand.Execute(vm.AvailableRooms[2]);
        var fourthRoom = vm.AvailableRooms[3];

        vm.AddRoomCommand.Execute(fourthRoom);

        Assert.Multiple(() =>
        {
            Assert.That(vm.ComparedRooms, Has.Count.EqualTo(3));
            Assert.That(fourthRoom.IsInComparison, Is.False);
            Assert.That(vm.StatusMessage, Does.Contain("tối đa 3 phòng"));
        });
    }

    [Test]
    public async Task AddRoomCommand_DuplicateRoom_DoesNotAddDuplicate()
    {
        var vm = new CompareRoomsViewModel(new MockListingsService());
        await vm.LoadAvailableRoomsCommand.ExecuteAsync(null);
        var existingRoom = vm.ComparedRooms[0];

        vm.AddRoomCommand.Execute(existingRoom);

        Assert.Multiple(() =>
        {
            Assert.That(vm.ComparedRooms, Has.Count.EqualTo(2));
            Assert.That(vm.StatusMessage, Does.Contain("đã có trong bảng so sánh"));
        });
    }

    [Test]
    public async Task RemoveRoomCommand_RemovesRoomAndShiftsSlots()
    {
        var vm = new CompareRoomsViewModel(new MockListingsService());
        await vm.LoadAvailableRoomsCommand.ExecuteAsync(null);
        var first = vm.Slot1!;
        var second = vm.Slot2!;

        vm.RemoveRoomCommand.Execute(first);

        Assert.Multiple(() =>
        {
            Assert.That(vm.ComparedRooms, Has.Count.EqualTo(1));
            Assert.That(vm.Slot1, Is.SameAs(second));
            Assert.That(vm.HasSlot2, Is.False);
            Assert.That(first.IsInComparison, Is.False);
            Assert.That(vm.HasBestValueSummary, Is.False);
        });
    }

    [Test]
    public async Task ClearComparisonCommand_ClearsComparedRoomsAndMatrixRows()
    {
        var vm = new CompareRoomsViewModel(new MockListingsService());
        await vm.LoadAvailableRoomsCommand.ExecuteAsync(null);

        vm.ClearComparisonCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.ComparedRooms, Is.Empty);
            Assert.That(vm.ComparisonRows, Is.Empty);
            Assert.That(vm.HasComparedRooms, Is.False);
            Assert.That(vm.HasNoComparedRooms, Is.True);
            Assert.That(vm.AvailableRooms, Has.All.Matches<RoomComparisonItemModel>(r => !r.IsInComparison));
        });
    }

    [Test]
    public async Task RebuildComparisonMatrix_HighlightsLowestRentLargestAreaAndClosestDistance()
    {
        var vm = new CompareRoomsViewModel(new MockListingsService());
        await vm.LoadAvailableRoomsCommand.ExecuteAsync(null);
        vm.ClearComparisonCommand.Execute(null);

        var r1 = new RoomComparisonItemModel { Id = "r1", Title = "R1", ShortTitle = "R1", Rent = 1_500_000m, Area = 18, DistanceMeters = 300, SecurityScore = 4.5 };
        var r2 = new RoomComparisonItemModel { Id = "r2", Title = "R2", ShortTitle = "R2", Rent = 2_200_000m, Area = 28, DistanceMeters = 150, SecurityScore = 4.9 };

        vm.AddRoomCommand.Execute(r1);
        vm.AddRoomCommand.Execute(r2);

        var rentRow = vm.ComparisonRows.First(r => r.AttributeKey == "rent");
        var areaRow = vm.ComparisonRows.First(r => r.AttributeKey == "area");
        var distRow = vm.ComparisonRows.First(r => r.AttributeKey == "distance");

        Assert.Multiple(() =>
        {
            Assert.That(r1.IsBestRent, Is.True);
            Assert.That(r2.IsBestRent, Is.False);
            Assert.That(r2.IsLargestArea, Is.True);
            Assert.That(r2.IsClosestToIctu, Is.True);
            Assert.That(rentRow.Slot1BgHex, Is.EqualTo("#DCFCE7"));
            Assert.That(rentRow.Slot2BgHex, Is.EqualTo("#F8FAFC"));
            Assert.That(areaRow.Slot2BgHex, Is.EqualTo("#DCFCE7"));
            Assert.That(distRow.Slot2BgHex, Is.EqualTo("#DCFCE7"));
        });
    }

    [Test]
    public async Task ShareComparisonImageCommand_RequiresAtLeastTwoRoomsAndInvokesEventWhenReady()
    {
        var vm = new CompareRoomsViewModel(new MockListingsService());
        int shareInvocations = 0;
        vm.ShareComparisonImageRequested += () =>
        {
            shareInvocations++;
            return Task.CompletedTask;
        };

        await vm.LoadAvailableRoomsCommand.ExecuteAsync(null);
        vm.RemoveRoomCommand.Execute(vm.Slot2); // còn 1 phòng

        await vm.ShareComparisonImageCommand.ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(shareInvocations, Is.EqualTo(0));
            Assert.That(vm.StatusMessage, Does.Contain("ít nhất 2 phòng"));
        });

        vm.AddRoomCommand.Execute(vm.AvailableRooms[2]); // đủ 2 phòng
        await vm.ShareComparisonImageCommand.ExecuteAsync(null);
        Assert.That(shareInvocations, Is.EqualTo(1));
    }

    [Test]
    public async Task LoadAvailableRoomsCommand_WhenServiceThrows_SetsErrorState()
    {
        var vm = new CompareRoomsViewModel(new FailingListingsService());

        await vm.LoadAvailableRoomsCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.HasError, Is.True);
            Assert.That(vm.ErrorMessage, Does.Contain("API unreachable"));
            Assert.That(vm.IsBusy, Is.False);
        });
    }
}
