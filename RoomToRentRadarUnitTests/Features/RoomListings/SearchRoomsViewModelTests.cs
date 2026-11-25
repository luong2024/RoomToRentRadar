using System.Globalization;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Localization;
using RoomToRentRadar.Models.RoomListings;
using RoomToRentRadar.Services.RoomListings;
using RoomToRentRadar.ViewModels.RoomListings;

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
    public void CreateSearchCriteria_WhenFilteringByAreaAndSortingByDistanceAsc_WithDistanceFilterInactive_AttachesIctuCoordinatesWithoutRadius()
    {
        // Arrange: User filters by Area and sorts by DistanceAsc while IsDistanceFilterActive is false
        var vm = CreateViewModel();
        vm.FilterMinAreaText = "20";
        vm.FilterMaxAreaText = "35";
        vm.IsDistanceFilterActive = false;

        // Act
        var criteria = vm.CreateSearchCriteria(sortBy: RoomSortOption.DistanceAsc);

        // Assert: Latitude and Longitude are attached so SearchRoomsUseCase doesn't fail with 400 Bad Request, but RadiusInKm is null
        Assert.Multiple(() =>
        {
            Assert.That(criteria.MinArea, Is.EqualTo(20));
            Assert.That(criteria.MaxArea, Is.EqualTo(35));
            Assert.That(criteria.SortBy, Is.EqualTo(RoomSortOption.DistanceAsc));
            Assert.That(criteria.RadiusInKm, Is.Null, "RadiusInKm must be null when distance filter is not active.");
            Assert.That(criteria.Latitude, Is.EqualTo(SearchRoomsViewModel.IctuLatitude), "Latitude must be attached when sorting by DistanceAsc.");
            Assert.That(criteria.Longitude, Is.EqualTo(SearchRoomsViewModel.IctuLongitude), "Longitude must be attached when sorting by DistanceAsc.");
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

    #region Fix 1 Integration: Server-Side Sorting via SortBy Query Parameter

    [Test]
    public async Task FilteringByArea_WithPriceAscSort_PassesSortByToServiceAndDisplaysSortedResults()
    {
        // Arrange: Service sorts by criteria.SortBy
        var allRooms = new List<RoomSummaryDto>
        {
            new("room-c", "Phòng C", 2_500_000m, 25, "private", true, 1.0),
            new("room-a", "Phòng A", 1_200_000m, 22, "private", true, 0.8),
            new("room-b", "Phòng B", 1_800_000m, 28, "private", true, 1.5)
        };
        _fakeService.SearchRoomsResultFactory = criteria =>
        {
            var sorted = criteria.SortBy switch
            {
                RoomSortOption.PriceAsc => allRooms.OrderBy(r => r.Price).ToList(),
                RoomSortOption.PriceDesc => allRooms.OrderByDescending(r => r.Price).ToList(),
                RoomSortOption.DistanceAsc => allRooms.OrderBy(r => r.DistanceInKm).ToList(),
                _ => allRooms
            };
            return new SearchRoomsResult(sorted, 1, 3, 3, ["wifi"]);
        };

        var vm = CreateViewModel();
        var priceAscOption = vm.SortOptions.First(o => o.Option == RoomSortOption.PriceAsc);
        await vm.SelectSortCommand.ExecuteAsync(priceAscOption);

        vm.FilterMinAreaText = "20";
        vm.FilterMaxAreaText = "30";

        // Act
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        // Assert: Service received SortBy = PriceAsc and VM contains sorted items
        Assert.Multiple(() =>
        {
            Assert.That(_fakeService.LastSearchCriteria?.SortBy, Is.EqualTo(RoomSortOption.PriceAsc));
            Assert.That(vm.Rooms, Has.Count.EqualTo(3));
            Assert.That(vm.Rooms[0].Price, Is.EqualTo(1_200_000m));
            Assert.That(vm.Rooms[1].Price, Is.EqualTo(1_800_000m));
            Assert.That(vm.Rooms[2].Price, Is.EqualTo(2_500_000m));
            Assert.That(vm.Rooms.Select(r => r.Price), Is.Ordered.Ascending);
        });
    }

    [Test]
    public async Task FilteringByArea_WithPriceDescSort_PassesSortByToServiceAndDisplaysSortedResults()
    {
        // Arrange
        var allRooms = new List<RoomSummaryDto>
        {
            new("room-a", "Phòng A", 1_200_000m, 22, "private", true, 0.8),
            new("room-c", "Phòng C", 2_500_000m, 25, "private", true, 1.0),
            new("room-b", "Phòng B", 1_800_000m, 28, "private", true, 1.5)
        };
        _fakeService.SearchRoomsResultFactory = criteria =>
        {
            var sorted = criteria.SortBy switch
            {
                RoomSortOption.PriceAsc => allRooms.OrderBy(r => r.Price).ToList(),
                RoomSortOption.PriceDesc => allRooms.OrderByDescending(r => r.Price).ToList(),
                RoomSortOption.DistanceAsc => allRooms.OrderBy(r => r.DistanceInKm).ToList(),
                _ => allRooms
            };
            return new SearchRoomsResult(sorted, 1, 3, 3, []);
        };

        var vm = CreateViewModel();
        var priceDescOption = vm.SortOptions.First(o => o.Option == RoomSortOption.PriceDesc);
        await vm.SelectSortCommand.ExecuteAsync(priceDescOption);

        vm.FilterMinAreaText = "20";

        // Act
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        // Assert: Service received SortBy = PriceDesc and VM contains descending items
        Assert.Multiple(() =>
        {
            Assert.That(_fakeService.LastSearchCriteria?.SortBy, Is.EqualTo(RoomSortOption.PriceDesc));
            Assert.That(vm.Rooms, Has.Count.EqualTo(3));
            Assert.That(vm.Rooms[0].Price, Is.EqualTo(2_500_000m));
            Assert.That(vm.Rooms[1].Price, Is.EqualTo(1_800_000m));
            Assert.That(vm.Rooms[2].Price, Is.EqualTo(1_200_000m));
            Assert.That(vm.Rooms.Select(r => r.Price), Is.Ordered.Descending);
        });
    }

    [Test]
    public async Task FilteringByDistance_WithDistanceAscSort_PassesSortByAndCoordinatesToService()
    {
        // Arrange
        var allRooms = new List<RoomSummaryDto>
        {
            new("room-far", "Phòng Xa", 1_500_000m, 20, "private", true, 3.2),
            new("room-close", "Phòng Gần", 1_500_000m, 20, "private", true, 0.4),
            new("room-mid", "Phòng Vừa", 1_500_000m, 20, "private", true, 1.5)
        };
        _fakeService.SearchRoomsResultFactory = criteria =>
        {
            var sorted = criteria.SortBy switch
            {
                RoomSortOption.PriceAsc => allRooms.OrderBy(r => r.Price).ToList(),
                RoomSortOption.PriceDesc => allRooms.OrderByDescending(r => r.Price).ToList(),
                RoomSortOption.DistanceAsc => allRooms.OrderBy(r => r.DistanceInKm).ToList(),
                _ => allRooms
            };
            return new SearchRoomsResult(sorted, 1, 3, 3, []);
        };

        var vm = CreateViewModel();
        var distanceAscOption = vm.SortOptions.First(o => o.Option == RoomSortOption.DistanceAsc);
        await vm.SelectSortCommand.ExecuteAsync(distanceAscOption);

        vm.IsDistanceFilterActive = true;
        vm.RadiusInKm = 5.0;

        // Act
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        // Assert: Service received SortBy = DistanceAsc and ordered items
        Assert.Multiple(() =>
        {
            Assert.That(_fakeService.LastSearchCriteria?.SortBy, Is.EqualTo(RoomSortOption.DistanceAsc));
            Assert.That(_fakeService.LastSearchCriteria?.Latitude, Is.EqualTo(SearchRoomsViewModel.IctuLatitude));
            Assert.That(_fakeService.LastSearchCriteria?.Longitude, Is.EqualTo(SearchRoomsViewModel.IctuLongitude));
            Assert.That(vm.Rooms, Has.Count.EqualTo(3));
            Assert.That(vm.Rooms[0].DistanceToCampusKm, Is.EqualTo(0.4));
            Assert.That(vm.Rooms[1].DistanceToCampusKm, Is.EqualTo(1.5));
            Assert.That(vm.Rooms[2].DistanceToCampusKm, Is.EqualTo(3.2));
            Assert.That(vm.Rooms.Select(r => r.DistanceToCampusKm), Is.Ordered.Ascending);
        });
    }

    [Test]
    public async Task FilteringByAreaAlone_WithDistanceAscSort_PassesCoordinatesToServiceAndLeavesRadiusNull()
    {
        // Arrange
        var allRooms = new List<RoomSummaryDto>
        {
            new("room-far", "Phòng Xa", 1_500_000m, 20, "private", true, 3.2),
            new("room-close", "Phòng Gần", 1_500_000m, 20, "private", true, 0.4),
            new("room-mid", "Phòng Vừa", 1_500_000m, 20, "private", true, 1.5)
        };
        _fakeService.SearchRoomsResultFactory = criteria =>
        {
            var sorted = criteria.SortBy switch
            {
                RoomSortOption.DistanceAsc => allRooms.OrderBy(r => r.DistanceInKm).ToList(),
                _ => allRooms
            };
            return new SearchRoomsResult(sorted, 1, 3, 3, []);
        };

        var vm = CreateViewModel();
        var distanceAscOption = vm.SortOptions.First(o => o.Option == RoomSortOption.DistanceAsc);
        await vm.SelectSortCommand.ExecuteAsync(distanceAscOption);

        vm.FilterMinAreaText = "15";
        vm.FilterMaxAreaText = "25";
        vm.IsDistanceFilterActive = false;

        // Act
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        // Assert: Service received SortBy = DistanceAsc and ICTU coordinates without RadiusInKm
        Assert.Multiple(() =>
        {
            Assert.That(_fakeService.LastSearchCriteria?.SortBy, Is.EqualTo(RoomSortOption.DistanceAsc));
            Assert.That(_fakeService.LastSearchCriteria?.MinArea, Is.EqualTo(15));
            Assert.That(_fakeService.LastSearchCriteria?.MaxArea, Is.EqualTo(25));
            Assert.That(_fakeService.LastSearchCriteria?.RadiusInKm, Is.Null, "RadiusInKm must not be set when IsDistanceFilterActive is false.");
            Assert.That(_fakeService.LastSearchCriteria?.Latitude, Is.EqualTo(SearchRoomsViewModel.IctuLatitude));
            Assert.That(_fakeService.LastSearchCriteria?.Longitude, Is.EqualTo(SearchRoomsViewModel.IctuLongitude));
        });
    }

    #endregion

    #region Fix 2, 3, 4: Reviewer Requirements (Reset, Mapping, Validation)

    [Test]
    public async Task ResetFiltersAsync_TrulyClearsAllFilters_AndSynchronizesWithServiceCriteria()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.SelectPricePreset("1m-2m");
        vm.FilterMinAreaText = "20";
        vm.FilterMaxAreaText = "35";
        vm.IsDistanceFilterActive = true;
        vm.RadiusInKm = 4.0;
        vm.SelectedRoomType = "shared";
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        Assert.That(vm.ActiveFilters, Is.Not.Empty);

        // Act
        await vm.ResetFiltersCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.MinPrice, Is.Null);
            Assert.That(vm.MaxPrice, Is.Null);
            Assert.That(vm.FilterMinPriceText, Is.Empty);
            Assert.That(vm.FilterMaxPriceText, Is.Empty);
            Assert.That(vm.SelectedPricePreset, Is.Empty);
            Assert.That(vm.MinArea, Is.Null);
            Assert.That(vm.MaxArea, Is.Null);
            Assert.That(vm.FilterMinAreaText, Is.Empty);
            Assert.That(vm.FilterMaxAreaText, Is.Empty);
            Assert.That(vm.SelectedRoomType, Is.EqualTo("all"));
            Assert.That(vm.IsDistanceFilterActive, Is.False);
            Assert.That(vm.ActiveFilters, Is.Empty);
            Assert.That(vm.HasActiveFilters, Is.False);

            // Criteria generated from reset VM is completely unfiltered
            var criteria = vm.CreateSearchCriteria();
            Assert.That(criteria.MinPrice, Is.Null);
            Assert.That(criteria.MaxPrice, Is.Null);
            Assert.That(criteria.MinArea, Is.Null);
            Assert.That(criteria.MaxArea, Is.Null);
            Assert.That(criteria.RoomType, Is.Null);
            Assert.That(criteria.RadiusInKm, Is.Null);
            Assert.That(criteria.HasActiveFilters, Is.False);
            Assert.That(criteria.ToQueryString(), Does.Not.Contain("minPrice"));
            Assert.That(criteria.ToQueryString(), Does.Not.Contain("maxPrice"));
            Assert.That(criteria.ToQueryString(), Does.Not.Contain("minArea"));
            Assert.That(criteria.ToQueryString(), Does.Not.Contain("maxArea"));
            Assert.That(criteria.ToQueryString(), Does.Not.Contain("roomType"));
            Assert.That(criteria.ToQueryString(), Does.Not.Contain("radiusInKm"));

            // Filter criteria sent to service reload is also unfiltered
            Assert.That(_fakeService.LastFilterCriteria?.MinPrice, Is.Null);
            Assert.That(_fakeService.LastFilterCriteria?.MaxPrice, Is.Null);
            Assert.That(_fakeService.LastFilterCriteria?.RoomType, Is.Null);
        });
    }

    [Test]
    public void RoomSummaryDto_ToListItemModel_AccuratelyMapsAllPropertiesWithoutHardcodedAddressOrCoercingNullDistance()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 11, 15, 10, 30, 0, TimeSpan.Zero);
        var dtoWithNullDistance = new RoomSummaryDto(
            Id: "room-101",
            Title: "Phòng Trọ Đẹp",
            Price: 1_800_000m,
            Area: 24.5,
            RoomType: "private",
            IsVerified: true,
            DistanceInKm: null,
            Address: "123 Đường Quang Trung, TP. Thái Nguyên",
            ThumbnailUrl: "https://example.com/img1.jpg",
            Amenities: ["Wifi", "Điều hòa"],
            CreatedAt: createdAt);

        // Act
        var model = dtoWithNullDistance.ToListItemModel();

        // Assert: DistanceInKm null is preserved (not coerced to 0), address & details faithfully mapped
        Assert.Multiple(() =>
        {
            Assert.That(model.Id, Is.EqualTo("room-101"));
            Assert.That(model.Title, Is.EqualTo("Phòng Trọ Đẹp"));
            Assert.That(model.Price, Is.EqualTo(1_800_000m));
            Assert.That(model.AreaSqm, Is.EqualTo(24.5));
            Assert.That(model.Address, Is.EqualTo("123 Đường Quang Trung, TP. Thái Nguyên"));
            Assert.That(model.DistanceToCampusKm, Is.Null, "DistanceToCampusKm must remain null when DistanceInKm is null");
            Assert.That(model.HasDistance, Is.False);
            Assert.That(model.FormattedDistance, Is.Empty);
            Assert.That(model.ThumbnailUrl, Is.EqualTo("https://example.com/img1.jpg"));
            Assert.That(model.Amenities, Is.EquivalentTo(new[] { "Wifi", "Điều hòa" }));
            Assert.That(model.CreatedAt, Is.EqualTo(createdAt));
            Assert.That(model.IsVerified, Is.True);
        });

        // Test with non-null distance
        var dtoWithDistance = dtoWithNullDistance with { DistanceInKm = 1.8 };
        var modelWithDistance = dtoWithDistance.ToListItemModel();
        Assert.Multiple(() =>
        {
            Assert.That(modelWithDistance.DistanceToCampusKm, Is.EqualTo(1.8));
            Assert.That(modelWithDistance.HasDistance, Is.True);
            Assert.That(modelWithDistance.FormattedDistance, Does.Contain("km to ICTU").Or.Contain("km tới ICTU"));
            Assert.That(modelWithDistance.FormattedDistance, Does.Contain("1,8").Or.Contain("1.8"));
        });
    }

    [Test]
    public void NegativePrice_TriggersHasPriceError_AndDisablesApplyCommand()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act: Enter negative min price
        vm.FilterMinPriceText = "-500,000";

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.MinPrice, Is.EqualTo(-500_000m));
            Assert.That(vm.HasPriceError, Is.True);
            Assert.That(vm.PriceErrorMessage, Does.Contain("không được nhỏ hơn 0"));
            Assert.That(vm.IsFilterValid, Is.False);
            Assert.That(vm.ApplyFiltersCommand.CanExecute(null), Is.False);
        });

        // Act: Fix price
        vm.FilterMinPriceText = "1,500,000";
        Assert.Multiple(() =>
        {
            Assert.That(vm.HasPriceError, Is.False);
            Assert.That(vm.IsFilterValid, Is.True);
            Assert.That(vm.ApplyFiltersCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public void NegativeArea_TriggersHasAreaError_AndDisablesApplyCommand()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act: Enter negative area
        vm.FilterMinAreaText = "-20";

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.MinArea, Is.EqualTo(-20.0));
            Assert.That(vm.HasAreaError, Is.True);
            Assert.That(vm.AreaErrorMessage, Does.Contain("không được nhỏ hơn 0"));
            Assert.That(vm.IsFilterValid, Is.False);
            Assert.That(vm.ApplyFiltersCommand.CanExecute(null), Is.False);
        });

        // Act: Fix area
        vm.FilterMinAreaText = "25";
        Assert.Multiple(() =>
        {
            Assert.That(vm.HasAreaError, Is.False);
            Assert.That(vm.IsFilterValid, Is.True);
            Assert.That(vm.ApplyFiltersCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public async Task ApplyFilters_WhenCombiningAreaOrDistanceWithSelectedAmenity_PreservesAmenitiesInSearchCriteria()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.FilterMinAreaText = "20";
        vm.IsDistanceFilterActive = true;
        vm.RadiusInKm = 5.0;
        vm.IsAmenityWifiSelected = true;
        vm.IsAmenityAirConSelected = true;

        _fakeService.SearchRoomsResultFactory = criteria => new SearchRoomsResult(
            Items: [],
            TotalCount: 5,
            TotalPages: 1,
            CurrentPage: 1,
            ActiveFilters: ["MinArea", "Distance", "Amenities"]);

        // Act
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_fakeService.LastSearchCriteria, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(_fakeService.LastSearchCriteria!.MinArea, Is.EqualTo(20.0));
            Assert.That(_fakeService.LastSearchCriteria.RadiusInKm, Is.EqualTo(5.0));
            Assert.That(_fakeService.LastSearchCriteria.Amenities, Is.Not.Null);
            Assert.That(_fakeService.LastSearchCriteria.Amenities, Does.Contain("wifi"));
            Assert.That(_fakeService.LastSearchCriteria.Amenities, Does.Contain("aircon"));
        });
    }

    [Test]
    public async Task ApplyFilters_WhenSearchReturnsZeroTotalCount_DisplaysZeroInsteadOfFallback24()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.FilterMinAreaText = "100";

        _fakeService.SearchRoomsResultFactory = criteria => new SearchRoomsResult(
            Items: [],
            TotalCount: 0,
            TotalPages: 0,
            CurrentPage: 1,
            ActiveFilters: ["MinArea"]);

        // Act
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(vm.TotalCount, Is.EqualTo(0));
            Assert.That(vm.TotalResultCount, Is.EqualTo(0));
            Assert.That(vm.FilterShowResultsText, Does.Contain("(0)"));
            Assert.That(vm.FilterShowResultsText, Does.Not.Contain("24"));
            Assert.That(vm.ResultSummaryText, Does.Contain("0"));
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

        public RoomFilterCriteria? LastFilterCriteria { get; private set; }

        public Func<RoomSearchCriteria, SearchRoomsResult>? SearchRoomsResultFactory { get; set; }

        public Task<PagedResponse<RoomListItemModel>> GetRoomListingsAsync(
            int page,
            int pageSize,
            RoomSortOption sortBy = RoomSortOption.Newest,
            string? search = null,
            RoomFilterCriteria? filter = null,
            CancellationToken cancellationToken = default)
        {
            LastFilterCriteria = filter;
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
