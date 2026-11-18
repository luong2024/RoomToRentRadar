using System.Globalization;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Localization;
using RoomToRentRadar.Features.RoomListings.Models;
using RoomToRentRadar.Features.RoomListings.Services;
using RoomToRentRadar.Features.RoomListings.ViewModels;

namespace RoomToRentRadarUnitTests.Features.RoomListings;

[TestFixture]
public class SearchRoomsViewModelTests
{
    private FakeRoomSearchService _fakeService = null!;
    private FakeErrorHandler _fakeErrorHandler = null!;
    private CultureInfo _initialCulture = null!;

    [SetUp]
    public void SetUp()
    {
        _initialCulture = LocalizationResourceManager.Instance.CurrentCulture;
        LocalizationResourceManager.Instance.SetCulture(new CultureInfo("vi"));
        _fakeService = new FakeRoomSearchService();
        _fakeErrorHandler = new FakeErrorHandler();
    }

    [TearDown]
    public void TearDown()
    {
        LocalizationResourceManager.Instance.SetCulture(_initialCulture);
    }

    #region Fix 1: Latitude/Longitude & Distance Filter Verification

    [Test]
    public void CreateSearchCriteria_WhenFilteringByAreaAlone_DoesNotSendRadiusOrCoordinates()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.FilterMinAreaText = "20";
        vm.FilterMaxAreaText = "35";

        // Act
        var criteria = vm.CreateSearchCriteria();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(criteria.MinArea, Is.EqualTo(20));
            Assert.That(criteria.MaxArea, Is.EqualTo(35));
            Assert.That(criteria.RadiusInKm, Is.Null, "RadiusInKm must be null when distance filter is not explicitly active.");
            Assert.That(criteria.Latitude, Is.Null, "Latitude must be null when distance filter is not active.");
            Assert.That(criteria.Longitude, Is.Null, "Longitude must be null when distance filter is not active.");
        });
    }

    [Test]
    public void CreateSearchCriteria_WhenFilteringByPriceAlone_DoesNotSendRadiusOrCoordinates()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.FilterMinPriceText = "1,000,000";
        vm.FilterMaxPriceText = "2,000,000";

        // Act
        var criteria = vm.CreateSearchCriteria();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(criteria.MinPrice, Is.EqualTo(1_000_000m));
            Assert.That(criteria.MaxPrice, Is.EqualTo(2_000_000m));
            Assert.That(criteria.RadiusInKm, Is.Null, "RadiusInKm must not be sent when only filtering by price.");
            Assert.That(criteria.Latitude, Is.Null);
            Assert.That(criteria.Longitude, Is.Null);
        });
    }

    [Test]
    public void CreateSearchCriteria_WhenFilteringByDistance_SendsRadiusWithIctuCoordinates()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.IsDistanceFilterActive = true;
        vm.RadiusInKm = 3.5;

        // Act
        var criteria = vm.CreateSearchCriteria();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(criteria.RadiusInKm, Is.EqualTo(3.5));
            Assert.That(criteria.Latitude, Is.EqualTo(SearchRoomsViewModel.IctuLatitude));
            Assert.That(criteria.Longitude, Is.EqualTo(SearchRoomsViewModel.IctuLongitude));
            Assert.That(criteria.Latitude, Is.EqualTo(21.5852));
            Assert.That(criteria.Longitude, Is.EqualTo(105.8073));
        });
    }

    [Test]
    public async Task ResetFilters_ResetsDistanceFilterToInactiveAndDefaultRadius()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.IsDistanceFilterActive = true;
        vm.RadiusInKm = 10.0;

        // Act
        await vm.ResetFiltersCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.IsDistanceFilterActive, Is.False);
            Assert.That(vm.RadiusInKm, Is.EqualTo(SearchRoomsViewModel.DefaultRadiusKm));
        });
    }

    [Test]
    public async Task RemoveActiveFilter_ForDistance_ResetsDistanceFilterToInactive()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.IsDistanceFilterActive = true;
        vm.RadiusInKm = 4.0;
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        var distanceChip = vm.ActiveFilters.FirstOrDefault(f => f.Key == "Distance");
        Assert.That(distanceChip, Is.Not.Null);

        // Act
        await vm.RemoveActiveFilterCommand.ExecuteAsync(distanceChip);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.IsDistanceFilterActive, Is.False);
            Assert.That(vm.ActiveFilters.Any(f => f.Key == "Distance"), Is.False);
        });
    }

    #endregion

    #region Fix 2: Bottom Sheet Overlay Synchronization & Closing

    [Test]
    public void CloseFilterPanelCommand_WhenExecuted_SetsBothVisibilityPropertiesToFalse()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.TogglePriceMenuCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsFilterPanelOpen, Is.True);
            Assert.That(vm.IsPriceMenuOpen, Is.True);
        });

        // Act
        vm.CloseFilterPanelCommand.Execute(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.IsFilterPanelOpen, Is.False, "CloseFilterPanelCommand must set IsFilterPanelOpen to false.");
            Assert.That(vm.IsPriceMenuOpen, Is.False, "CloseFilterPanelCommand must also synchronize IsPriceMenuOpen to false.");
        });
    }

    [Test]
    public async Task ApplyFiltersCommand_WhenExecuted_SetsBothVisibilityPropertiesToFalse()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.TogglePriceMenuCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsFilterPanelOpen, Is.True);
            Assert.That(vm.IsPriceMenuOpen, Is.True);
        });

        // Act
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.IsFilterPanelOpen, Is.False, "ApplyFiltersCommand must set IsFilterPanelOpen to false.");
            Assert.That(vm.IsPriceMenuOpen, Is.False, "ApplyFiltersCommand must synchronize IsPriceMenuOpen to false.");
        });
    }

    [Test]
    public async Task ApplyFilterSheetCommand_WhenExecuted_SetsBothVisibilityPropertiesToFalse()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.TogglePriceMenuCommand.Execute(null);

        // Act
        await vm.ApplyFilterSheetCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.IsFilterPanelOpen, Is.False);
            Assert.That(vm.IsPriceMenuOpen, Is.False);
        });
    }

    [Test]
    public void ClosePriceMenuCommand_WhenExecuted_SetsBothVisibilityPropertiesToFalse()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.IsFilterPanelOpen = true;

        Assert.That(vm.IsPriceMenuOpen, Is.True);

        // Act
        vm.ClosePriceMenuCommand.Execute(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.IsPriceMenuOpen, Is.False);
            Assert.That(vm.IsFilterPanelOpen, Is.False);
        });
    }

    [Test]
    public void TwoWaySync_WhenDirectlySettingEitherBoolean_KeepsBothInSync()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act 1: Set IsFilterPanelOpen
        vm.IsFilterPanelOpen = true;
        Assert.Multiple(() =>
        {
            Assert.That(vm.IsFilterPanelOpen, Is.True);
            Assert.That(vm.IsPriceMenuOpen, Is.True);
        });

        // Act 2: Set IsPriceMenuOpen false
        vm.IsPriceMenuOpen = false;
        Assert.Multiple(() =>
        {
            Assert.That(vm.IsFilterPanelOpen, Is.False);
            Assert.That(vm.IsPriceMenuOpen, Is.False);
        });
    }

    #endregion

    #region Fix 3: Client-Side LINQ Sorting Preservation

    [Test]
    public async Task FilteringByArea_WithPriceAscSort_CorrectlySortsItemsAscendingOnClient()
    {
        // Arrange: Service returns un-sorted rooms from backend search
        var searchRooms = new List<RoomSummaryDto>
        {
            new("room-c", "Phòng C", 2_500_000m, 25, "private", true, 1.0),
            new("room-a", "Phòng A", 1_200_000m, 22, "private", true, 0.8),
            new("room-b", "Phòng B", 1_800_000m, 28, "private", true, 1.5)
        };
        _fakeService.SearchRoomsResultFactory = _ => new SearchRoomsResult(searchRooms, 1, 3, 3, ["wifi"]);

        var vm = CreateViewModel();
        var priceAscOption = vm.SortOptions.First(o => o.Option == RoomSortOption.PriceAsc);
        await vm.SelectSortCommand.ExecuteAsync(priceAscOption);

        vm.FilterMinAreaText = "20";
        vm.FilterMaxAreaText = "30";

        // Act
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        // Assert: Client applied OrderBy(r => r.Price)
        Assert.Multiple(() =>
        {
            Assert.That(vm.Rooms, Has.Count.EqualTo(3));
            Assert.That(vm.Rooms[0].Price, Is.EqualTo(1_200_000m));
            Assert.That(vm.Rooms[1].Price, Is.EqualTo(1_800_000m));
            Assert.That(vm.Rooms[2].Price, Is.EqualTo(2_500_000m));
            Assert.That(vm.Rooms.Select(r => r.Price), Is.Ordered.Ascending);
        });
    }

    [Test]
    public async Task FilteringByArea_WithPriceDescSort_CorrectlySortsItemsDescendingOnClient()
    {
        // Arrange
        var searchRooms = new List<RoomSummaryDto>
        {
            new("room-a", "Phòng A", 1_200_000m, 22, "private", true, 0.8),
            new("room-c", "Phòng C", 2_500_000m, 25, "private", true, 1.0),
            new("room-b", "Phòng B", 1_800_000m, 28, "private", true, 1.5)
        };
        _fakeService.SearchRoomsResultFactory = _ => new SearchRoomsResult(searchRooms, 1, 3, 3, []);

        var vm = CreateViewModel();
        var priceDescOption = vm.SortOptions.First(o => o.Option == RoomSortOption.PriceDesc);
        await vm.SelectSortCommand.ExecuteAsync(priceDescOption);

        vm.FilterMinAreaText = "20";

        // Act
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        // Assert: Client applied OrderByDescending(r => r.Price)
        Assert.Multiple(() =>
        {
            Assert.That(vm.Rooms, Has.Count.EqualTo(3));
            Assert.That(vm.Rooms[0].Price, Is.EqualTo(2_500_000m));
            Assert.That(vm.Rooms[1].Price, Is.EqualTo(1_800_000m));
            Assert.That(vm.Rooms[2].Price, Is.EqualTo(1_200_000m));
            Assert.That(vm.Rooms.Select(r => r.Price), Is.Ordered.Descending);
        });
    }

    [Test]
    public async Task FilteringByDistance_WithDistanceAscSort_CorrectlySortsItemsByDistanceOnClient()
    {
        // Arrange
        var searchRooms = new List<RoomSummaryDto>
        {
            new("room-far", "Phòng Xa", 1_500_000m, 20, "private", true, 3.2),
            new("room-close", "Phòng Gần", 1_500_000m, 20, "private", true, 0.4),
            new("room-mid", "Phòng Vừa", 1_500_000m, 20, "private", true, 1.5)
        };
        _fakeService.SearchRoomsResultFactory = _ => new SearchRoomsResult(searchRooms, 1, 3, 3, []);

        var vm = CreateViewModel();
        var distanceAscOption = vm.SortOptions.First(o => o.Option == RoomSortOption.DistanceAsc);
        await vm.SelectSortCommand.ExecuteAsync(distanceAscOption);

        vm.IsDistanceFilterActive = true;
        vm.RadiusInKm = 5.0;

        // Act
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        // Assert: Client applied OrderBy(r => r.DistanceToCampusKm)
        Assert.Multiple(() =>
        {
            Assert.That(vm.Rooms, Has.Count.EqualTo(3));
            Assert.That(vm.Rooms[0].DistanceToCampusKm, Is.EqualTo(0.4));
            Assert.That(vm.Rooms[1].DistanceToCampusKm, Is.EqualTo(1.5));
            Assert.That(vm.Rooms[2].DistanceToCampusKm, Is.EqualTo(3.2));
            Assert.That(vm.Rooms.Select(r => r.DistanceToCampusKm), Is.Ordered.Ascending);
        });
    }

    #endregion

    private RoomListingsViewModel CreateViewModel(int pageSize = 10) =>
        new(_fakeService, _fakeErrorHandler)
        {
            PageSize = pageSize
        };

    private sealed class FakeRoomSearchService : IRoomListingsService
    {
        public RoomSearchCriteria? LastSearchCriteria { get; private set; }

        public Func<RoomSearchCriteria, SearchRoomsResult>? SearchRoomsResultFactory { get; set; }

        public Task<PagedResponse<RoomListItemModel>> GetRoomListingsAsync(
            int page,
            int pageSize,
            RoomSortOption sortBy = RoomSortOption.Newest,
            string? search = null,
            RoomFilterCriteria? filter = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PagedResponse<RoomListItemModel>([], page, pageSize, 0));
        }

        public Task<RoomListItemModel?> GetRoomByIdAsync(
            string roomId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<RoomListItemModel?>(null);

        public Task<SearchRoomsResult> SearchRoomsAsync(
            RoomSearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            LastSearchCriteria = criteria;
            if (SearchRoomsResultFactory is not null)
            {
                return Task.FromResult(SearchRoomsResultFactory(criteria));
            }

            return Task.FromResult(new SearchRoomsResult([], 0, 0, criteria.PageNumber, []));
        }
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public List<Exception> HandledExceptions { get; } = [];

        public void HandleError(Exception ex) => HandledExceptions.Add(ex);
    }
}
