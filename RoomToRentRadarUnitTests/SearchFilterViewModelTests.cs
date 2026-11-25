using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Services;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class SearchFilterViewModelTests
{
    private sealed class RecordingNavigationService : IAppNavigationService
    {
        public string? NavigatedRoomId { get; private set; }
        public int NavigateCount { get; private set; }

        public Task NavigateToRoomDetailAsync(string roomId, bool? isAvailable = null)
        {
            NavigatedRoomId = roomId;
            NavigateCount++;
            return Task.CompletedTask;
        }

        public Task ShowAlertAsync(string title, string message, string cancel = "Đóng") => Task.CompletedTask;
    }

    private sealed class LiveFlagSearchFilterService(bool isFromLiveApi) : ISearchFilterService
    {
        private readonly MockSearchFilterService _inner = new();

        public async Task<SearchFilterFetchResult> SearchAsync(
            SearchListingsQueryContract query,
            CancellationToken cancellationToken = default)
        {
            var inner = await _inner.SearchAsync(query, cancellationToken);
            return new SearchFilterFetchResult(inner.Response, isFromLiveApi);
        }
    }

    [Test]
    public void Constructor_LoadsDefaultCatalogAndTotalCount()
    {
        var vm = new SearchFilterViewModel(new MockSearchFilterService());

        Assert.Multiple(() =>
        {
            Assert.That(vm.TotalResultCount, Is.EqualTo(8));
            Assert.That(vm.FilteredRooms, Has.Count.EqualTo(8));
            Assert.That(vm.HasActiveFilters, Is.False);
            Assert.That(vm.HasNoResults, Is.False);
            Assert.That(vm.ResultCountText, Is.EqualTo("Tìm thấy 8 phòng phù hợp"));
            Assert.That(vm.DataSourceBadgeText, Does.Contain("Offline"));
        });
    }

    [Test]
    public void FilterByPriceRange_FiltersCatalogAndCreatesActiveChips()
    {
        var vm = new SearchFilterViewModel(new MockSearchFilterService());

        vm.MinRentText = "1500000";
        vm.MaxRentText = "2000000";

        Assert.Multiple(() =>
        {
            Assert.That(vm.FilteredRooms, Has.Count.EqualTo(4));
            Assert.That(vm.TotalResultCount, Is.EqualTo(4));
            Assert.That(vm.HasActiveFilters, Is.True);
            Assert.That(vm.ActiveFilterChips.Select(c => c.Key), Is.EquivalentTo(["minRent", "maxRent"]));
            Assert.That(vm.FilteredRooms, Has.All.Matches<FilteredRoomCardModel>(r => r.Rent is >= 1_500_000m and <= 2_000_000m));
        });
    }

    [Test]
    public void FilterByRoomType_PhongKhepKin_MatchesExpectedRooms()
    {
        var vm = new SearchFilterViewModel(new MockSearchFilterService());

        vm.SelectedRoomType = "Phòng khép kín";

        Assert.Multiple(() =>
        {
            Assert.That(vm.FilteredRooms, Has.Count.EqualTo(2));
            Assert.That(vm.TotalResultCount, Is.EqualTo(2));
            Assert.That(vm.ActiveFilterChips.Any(c => c.Key == "roomType"), Is.True);
            Assert.That(vm.FilteredRooms, Has.All.Matches<FilteredRoomCardModel>(r => r.RoomType.Contains("khép kín", StringComparison.OrdinalIgnoreCase)));
        });
    }

    [Test]
    public void FilterByAreaPresetAndDistance_CombinesAllActiveCriteria()
    {
        var vm = new SearchFilterViewModel(new MockSearchFilterService());

        vm.ApplyAreaPresetCommand.Execute("standard"); // 20 - 28 m2
        vm.MaxDistanceMeters = 500;

        Assert.Multiple(() =>
        {
            Assert.That(vm.FilteredRooms, Has.Count.EqualTo(2));
            Assert.That(vm.FilteredRooms, Has.All.Matches<FilteredRoomCardModel>(
                r => r.Area is >= 20 and <= 28 && r.DistanceMeters <= 500));
            Assert.That(vm.ActiveFilterChips.Select(c => c.Key), Is.EquivalentTo(["area", "distance"]));
        });
    }

    [Test]
    public void RemoveFilterChipCommand_RemovesOnlyTargetFilter()
    {
        var vm = new SearchFilterViewModel(new MockSearchFilterService());
        vm.SelectedRoomType = "Studio";
        vm.MaxDistanceMeters = 500;
        var roomTypeChip = vm.ActiveFilterChips.First(c => c.Key == "roomType");

        vm.RemoveFilterChipCommand.Execute(roomTypeChip);

        Assert.Multiple(() =>
        {
            Assert.That(vm.SelectedRoomType, Is.EqualTo("Tất cả"));
            Assert.That(vm.MaxDistanceMeters, Is.EqualTo(500));
            Assert.That(vm.ActiveFilterChips, Has.Count.EqualTo(1));
            Assert.That(vm.ActiveFilterChips[0].Key, Is.EqualTo("distance"));
        });
    }

    [Test]
    public async Task ClearFiltersCommand_ResetsFiltersAndRestoresAllEightRooms()
    {
        var vm = new SearchFilterViewModel(new MockSearchFilterService());
        vm.MinRentText = "2500000";
        vm.SelectedRoomType = "Chung cư mini";
        vm.MaxDistanceMeters = 1000;

        await vm.ClearFiltersCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.MinRentText, Is.Empty);
            Assert.That(vm.MaxRentText, Is.Empty);
            Assert.That(vm.SelectedRoomType, Is.EqualTo("Tất cả"));
            Assert.That(vm.MaxDistanceMeters, Is.EqualTo(3000));
            Assert.That(vm.ActiveFilterChips, Is.Empty);
            Assert.That(vm.HasActiveFilters, Is.False);
            Assert.That(vm.TotalResultCount, Is.EqualTo(8));
            Assert.That(vm.FilteredRooms, Has.Count.EqualTo(8));
        });
    }

    [Test]
    public void SelectedSortOption_PriceAsc_OrdersRoomsByAscendingRent()
    {
        var vm = new SearchFilterViewModel(new MockSearchFilterService());

        vm.SelectedSortOption = vm.SortOptions[1]; // price_asc
        var rents = vm.FilteredRooms.Select(r => r.Rent).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(rents, Has.Count.EqualTo(8));
            Assert.That(rents, Is.Ordered);
            Assert.That(rents[0], Is.EqualTo(1_200_000m));
        });
    }

    [Test]
    public void FilterWithNoMatches_SetsZeroCountAndEmptyState()
    {
        var vm = new SearchFilterViewModel(new MockSearchFilterService());

        vm.MinRentText = "9000000";

        Assert.Multiple(() =>
        {
            Assert.That(vm.TotalResultCount, Is.EqualTo(0));
            Assert.That(vm.FilteredRooms, Is.Empty);
            Assert.That(vm.HasNoResults, Is.True);
            Assert.That(vm.ResultCountText, Is.EqualTo("Tìm thấy 0 phòng phù hợp"));
        });
    }

    [Test]
    public async Task OpenRoomDetailCommand_NavigatesWithRoomIdAndLiveApiBadgeUpdates()
    {
        var nav = new RecordingNavigationService();
        var vm = new SearchFilterViewModel(new LiveFlagSearchFilterService(isFromLiveApi: true), nav);
        var room = vm.FilteredRooms[0];

        await vm.OpenRoomDetailCommand.ExecuteAsync(room);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsFromLiveApi, Is.True);
            Assert.That(vm.DataSourceBadgeText, Does.Contain("API T04.1"));
            Assert.That(nav.NavigateCount, Is.EqualTo(1));
            Assert.That(nav.NavigatedRoomId, Is.EqualTo(room.Id));
        });
    }
}
