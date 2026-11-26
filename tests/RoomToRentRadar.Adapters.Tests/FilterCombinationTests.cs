namespace RoomToRentRadar.Adapters.Tests;

using NUnit.Framework;
using RoomToRentRadar.Adapters.Outbound.Persistence.InMemory;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases;
using RoomToRentRadar.Application.UseCases.RoomListings;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using ListingRoomStatus = RoomToRentRadar.Domain.Enums.RoomStatus;
using RoomEntityStatus = RoomToRentRadar.Domain.Entities.RoomStatus;

/// <summary>
/// Task T04.3 - Filter Combination & Regression Tests against existing T04.1/T01.1 implementations.
/// </summary>
[TestFixture]
public class FilterCombinationTests
{
    private InMemoryRoomRepository _roomRepository = null!;
    private FakeCampusRepository _campusRepository = null!;
    private FakeCacheService _cacheService = null!;
    private FakeRoomListingRepository _roomListingRepository = null!;
    private SearchRoomsUseCase _searchRoomsUseCase = null!;
    private GetRoomListingsUseCase _getRoomListingsUseCase = null!;

    private readonly Campus _ictuCampus = new(
        "campus-ictu",
        "Trường ĐH Công Nghệ Thông Tin và Truyền Thông",
        "ICTU",
        new GeoLocation(21.5852, 105.8073),
        "Đường Z115, Quyết Thắng, Thái Nguyên");

    [SetUp]
    public void SetUp()
    {
        _roomRepository = new InMemoryRoomRepository();
        _campusRepository = new FakeCampusRepository();
        _cacheService = new FakeCacheService();
        _roomListingRepository = new FakeRoomListingRepository();

        _campusRepository.AddAsync(_ictuCampus).GetAwaiter().GetResult();

        _searchRoomsUseCase = new SearchRoomsUseCase(_roomRepository);
        _getRoomListingsUseCase = new GetRoomListingsUseCase(
            _roomListingRepository,
            _campusRepository,
            _cacheService);

        SeedStandardRoomDataset();
    }

    private void SeedStandardRoomDataset()
    {
        var rooms = new List<Room>
        {
            new()
            {
                Id = "room-001",
                Title = "Phòng trọ giá rẻ gần cổng trường ICTU",
                Description = "Phòng khép kín đầy đủ wifi, giờ giấc tự do, an ninh tốt",
                Price = 1_200_000m,
                Area = 15.0,
                Address = "Số 10 ngõ 5, Đường Z115, Thái Nguyên",
                Latitude = 21.5860,
                Longitude = 105.8080,
                Status = RoomEntityStatus.Available,
                IsVerified = true,
                RoomType = "private",
                Amenities = ["wifi", "parking"],
                ThumbnailUrl = "https://cdn.example.com/r1.jpg",
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new()
            {
                Id = "room-002",
                Title = "Phòng khép kín có điều hòa Z115",
                Description = "Phòng trọ cao cấp có điều hòa, bình nóng lạnh, máy giặt chung",
                Price = 2_200_000m,
                Area = 22.0,
                Address = "Số 45 Đường Z115, Quyết Thắng, Thái Nguyên",
                Latitude = 21.5880,
                Longitude = 105.8120,
                Status = RoomEntityStatus.Available,
                IsVerified = true,
                RoomType = "private",
                Amenities = ["wifi", "aircon", "bathroom"],
                ThumbnailUrl = "https://cdn.example.com/r2.jpg",
                CreatedAt = DateTime.UtcNow.AddDays(-8)
            },
            new()
            {
                Id = "room-003",
                Title = "Phòng ở ghép rộng rãi Tân Thịnh",
                Description = "Phòng rộng cho sinh viên ở ghép, có điều hòa và máy giặt",
                Price = 1_600_000m,
                Area = 30.0,
                Address = "Tổ 7, Phường Tân Thịnh, Thái Nguyên",
                Latitude = 21.5720,
                Longitude = 105.8220,
                Status = RoomEntityStatus.Available,
                IsVerified = true,
                RoomType = "shared",
                Amenities = ["wifi", "aircon", "washing"],
                ThumbnailUrl = "https://cdn.example.com/r3.jpg",
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new()
            {
                Id = "room-004",
                Title = "Căn hộ Studio cao cấp Quang Trung",
                Description = "Căn hộ mini đầy đủ nội thất, điều hòa, bếp riêng",
                Price = 3_800_000m,
                Area = 35.0,
                Address = "Đường Quang Trung, TP Thái Nguyên",
                Latitude = 21.5920,
                Longitude = 105.8350,
                Status = RoomEntityStatus.Available,
                IsVerified = true,
                RoomType = "apartment",
                Amenities = ["wifi", "aircon", "bathroom", "kitchen", "washing"],
                ThumbnailUrl = "https://cdn.example.com/r4.jpg",
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            },
            new()
            {
                Id = "room-005",
                Title = "Phòng trọ thoáng mát Tích Lương",
                Description = "Khu vực yên tĩnh, gần bến xe",
                Price = 2_500_000m,
                Area = 25.0,
                Address = "Phường Tích Lương, Thái Nguyên",
                Latitude = 21.5400,
                Longitude = 105.8500,
                Status = RoomEntityStatus.Available,
                IsVerified = true,
                RoomType = "private",
                Amenities = ["wifi", "parking", "kitchen"],
                ThumbnailUrl = "https://cdn.example.com/r5.jpg",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                Id = "room-006-occupied",
                Title = "Phòng trọ đã có người thuê",
                Description = "Không được hiển thị",
                Price = 1_500_000m,
                Area = 20.0,
                Address = "Đường Z115, Thái Nguyên",
                Latitude = 21.5855,
                Longitude = 105.8075,
                Status = RoomEntityStatus.Rented,
                IsVerified = true,
                RoomType = "private",
                Amenities = ["wifi"]
            },
            new()
            {
                Id = "room-007-unverified",
                Title = "Phòng trọ chưa xác thực",
                Description = "Đang chờ duyệt",
                Price = 1_000_000m,
                Area = 20.0,
                Address = "Đường Z115, Thái Nguyên",
                Latitude = 21.5855,
                Longitude = 105.8075,
                Status = RoomEntityStatus.Available,
                IsVerified = false,
                RoomType = "private",
                Amenities = ["wifi"]
            }
        };

        foreach (var room in rooms)
        {
            _roomRepository.Upsert(room);
            _roomListingRepository.AddAsync(new RoomListing(
                id: room.Id,
                title: room.Title,
                description: room.Description,
                monthlyRent: new Money(room.Price, "VND"),
                areaSquareMeters: room.Area,
                address: string.IsNullOrWhiteSpace(room.Address) ? "Thái Nguyên" : room.Address,
                location: new GeoLocation(room.Latitude ?? 0, room.Longitude ?? 0),
                status: room.Status == RoomEntityStatus.Available ? ListingRoomStatus.Available : ListingRoomStatus.Occupied,
                verificationStatus: room.IsVerified ? VerificationStatus.Verified : VerificationStatus.Pending,
                campusId: _ictuCampus.Id,
                thumbnailUrl: room.ThumbnailUrl,
                createdAtUtc: room.CreatedAt)).GetAwaiter().GetResult();
        }
    }

    #region 1. Baseline US01 Browse Regression & Default Search

    [Test]
    public async Task Case1_BaselineBrowse_V1Rooms_ReturnsAvailableAndVerifiedSortedByDistance()
    {
        var query = new GetRoomListingsQuery(CampusId: "campus-ictu", Page: 1, PageSize: 10);
        var result = await _getRoomListingsUseCase.ExecuteAsync(query);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCount, Is.EqualTo(5));
            Assert.That(result.Items, Has.Count.EqualTo(5));
            Assert.That(result.Items.All(r => r.IsVerified), Is.True);
            Assert.That(result.Items.Any(r => r.Id == "room-006-occupied"), Is.False);
            Assert.That(result.Items.Any(r => r.Id == "room-007-unverified"), Is.False);
            Assert.That(result.Items.Select(x => x.DistanceToCampusKm), Is.Ordered.Ascending);
        });
    }

    [Test]
    public async Task Case1b_DefaultSearch_NoFilters_ReturnsAllAvailableVerifiedRooms()
    {
        var result = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(pageNumber: 1, pageSize: 10));

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCount, Is.EqualTo(5));
            Assert.That(result.Items, Has.Count.EqualTo(5));
            Assert.That(result.ActiveFilters, Is.Empty);
        });
    }

    #endregion

    #region 2. Keyword Search

    [Test]
    public async Task Case2_KeywordSearch_SingleAndCombinedWithPriceFilter()
    {
        var single = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(keyword: "điều hòa"));
        Assert.Multiple(() =>
        {
            Assert.That(single.TotalCount, Is.EqualTo(3));
            Assert.That(single.Items.Select(x => x.Id), Is.EquivalentTo(["room-002", "room-003", "room-004"]));
        });

        var combined = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(keyword: "điều hòa", maxPrice: 2_500_000m));
        Assert.Multiple(() =>
        {
            Assert.That(combined.TotalCount, Is.EqualTo(2));
            Assert.That(combined.Items.Select(x => x.Id), Is.EquivalentTo(["room-002", "room-003"]));
        });
    }

    #endregion

    #region 3. Price Range Filter (AC1)

    [Test]
    public async Task Case3_PriceRange_MinMaxAndBoundaryValues()
    {
        var minOnly = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(minPrice: 2_500_000m));
        Assert.That(minOnly.Items.Select(x => x.Id), Is.EquivalentTo(["room-004", "room-005"]));

        var maxOnly = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(maxPrice: 1_600_000m));
        Assert.That(maxOnly.Items.Select(x => x.Id), Is.EquivalentTo(["room-001", "room-003"]));

        var range = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(minPrice: 1_600_000m, maxPrice: 2_500_000m));
        Assert.That(range.Items.Select(x => x.Id), Is.EquivalentTo(["room-002", "room-003", "room-005"]));

        var exactBoundary = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(minPrice: 2_200_000m, maxPrice: 2_200_000m));
        Assert.That(exactBoundary.Items.Select(x => x.Id), Is.EqualTo(new[] { "room-002" }));
    }

    #endregion

    #region 4. Area Range Filter (AC1)

    [Test]
    public async Task Case4_AreaRange_MinMaxAndBoundaryValues()
    {
        var minOnly = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(minArea: 30.0));
        Assert.That(minOnly.Items.Select(x => x.Id), Is.EquivalentTo(["room-003", "room-004"]));

        var maxOnly = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(maxArea: 22.0));
        Assert.That(maxOnly.Items.Select(x => x.Id), Is.EquivalentTo(["room-001", "room-002"]));

        var range = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(minArea: 22.0, maxArea: 30.0));
        Assert.That(range.Items.Select(x => x.Id), Is.EquivalentTo(["room-002", "room-003", "room-005"]));
    }

    #endregion

    #region 5. RoomType Filter (AC1)

    [Test]
    public async Task Case5_RoomType_FiltersAccurately()
    {
        var shared = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(roomType: "shared"));
        Assert.That(shared.Items.Select(x => x.Id), Is.EqualTo(new[] { "room-003" }));

        var apartment = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(roomType: "apartment"));
        Assert.That(apartment.Items.Select(x => x.Id), Is.EqualTo(new[] { "room-004" }));
    }

    #endregion

    #region 6. Maximum Distance Filter (AC1)

    [Test]
    public async Task Case6_DistanceFilter_ActiveRadiusVsNoRadius()
    {
        var withoutRadius = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
            latitude: _ictuCampus.Location.Latitude,
            longitude: _ictuCampus.Location.Longitude,
            radiusInKm: null));
        Assert.That(withoutRadius.TotalCount, Is.EqualTo(5));

        var within1Km = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
            latitude: _ictuCampus.Location.Latitude,
            longitude: _ictuCampus.Location.Longitude,
            radiusInKm: 1.0));
        Assert.That(within1Km.Items.Select(x => x.Id), Is.EquivalentTo(["room-001", "room-002"]));
    }

    #endregion

    #region 7. Two-Condition Combinations (AC2)

    [Test]
    public async Task Case7_TwoConditions_PricePlusArea_PricePlusDistance_PricePlusRoomType_AreaPlusDistance()
    {
        var pricePlusArea = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
            minPrice: 1_500_000m, maxPrice: 2_600_000m,
            minArea: 20.0, maxArea: 26.0));
        Assert.That(pricePlusArea.Items.Select(x => x.Id), Is.EquivalentTo(["room-002", "room-005"]));

        var pricePlusDistance = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
            maxPrice: 2_500_000m,
            latitude: _ictuCampus.Location.Latitude,
            longitude: _ictuCampus.Location.Longitude,
            radiusInKm: 3.0));
        Assert.That(pricePlusDistance.Items.Select(x => x.Id), Is.EquivalentTo(["room-001", "room-002", "room-003"]));

        var pricePlusRoomType = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
            minPrice: 1_500_000m, maxPrice: 2_500_000m,
            roomType: "private"));
        Assert.That(pricePlusRoomType.Items.Select(x => x.Id), Is.EquivalentTo(["room-002", "room-005"]));

        var areaPlusDistance = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
            minArea: 20.0,
            latitude: _ictuCampus.Location.Latitude,
            longitude: _ictuCampus.Location.Longitude,
            radiusInKm: 3.0));
        Assert.That(areaPlusDistance.Items.Select(x => x.Id), Is.EquivalentTo(["room-002", "room-003", "room-004"]));
    }

    #endregion

    #region 8. Three & Four Condition Combinations (AC2)

    [Test]
    public async Task Case8_ThreeAndFourConditions_FiltersConjunctively()
    {
        var threeConditions = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
            minPrice: 1_500_000m, maxPrice: 3_000_000m,
            minArea: 20.0,
            roomType: "private"));
        Assert.That(threeConditions.Items.Select(x => x.Id), Is.EquivalentTo(["room-002", "room-005"]));

        var fourConditions = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
            minPrice: 1_500_000m, maxPrice: 3_000_000m,
            minArea: 20.0,
            roomType: "private",
            latitude: _ictuCampus.Location.Latitude,
            longitude: _ictuCampus.Location.Longitude,
            radiusInKm: 3.0));
        Assert.That(fourConditions.Items.Select(x => x.Id), Is.EqualTo(new[] { "room-002" }));
    }

    #endregion

    #region 9. Combinations with Amenities (AC2)

    [Test]
    public async Task Case9_CombinedFiltersWithAmenities_DoesNotDropAmenities()
    {
        var result = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
            maxPrice: 3_000_000m,
            minArea: 20.0,
            amenities: ["wifi", "aircon"]));

        Assert.That(result.Items.Select(x => x.Id), Is.EquivalentTo(["room-002", "room-003"]));
        Assert.That(result.Items.All(x => x.Amenities != null && x.Amenities.Contains("wifi") && x.Amenities.Contains("aircon")), Is.True);
    }

    #endregion

    #region 10. Filter + Sorting + Pagination (AC2 & AC3)

    [Test]
    public async Task Case10_FilterWithSortingAndPagination_PreservesFiltersAcrossPages()
    {
        var page1 = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
            minArea: 20.0, sortBy: "price_desc", pageNumber: 1, pageSize: 2));

        var page2 = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
            minArea: 20.0, sortBy: "price_desc", pageNumber: 2, pageSize: 2));

        Assert.Multiple(() =>
        {
            Assert.That(page1.TotalCount, Is.EqualTo(4));
            Assert.That(page1.Items.Select(x => x.Id), Is.EqualTo(new[] { "room-004", "room-005" }));
            Assert.That(page2.TotalCount, Is.EqualTo(4));
            Assert.That(page2.Items.Select(x => x.Id), Is.EqualTo(new[] { "room-002", "room-003" }));
        });
    }

    #endregion

    #region 11. Zero Results Handling

    [Test]
    public async Task Case11_ZeroResults_ReturnsEmptyListAndZeroCountWithoutFallback()
    {
        var result = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(minPrice: 50_000_000m));

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCount, Is.EqualTo(0));
            Assert.That(result.Items, Is.Empty);
        });
    }

    #endregion

    #region 12. Validation for Invalid Inputs

    [Test]
    public void Case12_InvalidInputs_ThrowsArgumentException()
    {
        Assert.ThrowsAsync<ArgumentException>(() => _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(minPrice: -100m)));
        Assert.ThrowsAsync<ArgumentException>(() => _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(minArea: -5.0)));
        Assert.ThrowsAsync<ArgumentException>(() => _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(minPrice: 5_000_000m, maxPrice: 2_000_000m)));
        Assert.ThrowsAsync<ArgumentException>(() => _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(minArea: 30.0, maxArea: 15.0)));
    }

    #endregion

    #region 13. Clear Filters Restores Full List (AC4)

    [Test]
    public async Task Case13_ClearFilters_RestoresFullAvailableVerifiedCatalog()
    {
        var filtered = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(roomType: "apartment", minPrice: 3_000_000m));
        Assert.That(filtered.TotalCount, Is.EqualTo(1));

        var cleared = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery());
        Assert.Multiple(() =>
        {
            Assert.That(cleared.TotalCount, Is.EqualTo(5));
            Assert.That(cleared.ActiveFilters, Is.Empty);
        });
    }

    #endregion

    #region 14. Result DTO Completeness

    [Test]
    public async Task Case14_SearchDto_ContainsAllMandatoryFieldsFromRealEntities()
    {
        var result = await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
            keyword: "rẻ",
            latitude: _ictuCampus.Location.Latitude,
            longitude: _ictuCampus.Location.Longitude));

        Assert.That(result.Items, Is.Not.Empty);
        var item = result.Items[0];

        Assert.Multiple(() =>
        {
            Assert.That(item.Id, Is.EqualTo("room-001"));
            Assert.That(item.Title, Is.Not.Null.And.Not.Empty);
            Assert.That(item.Price, Is.EqualTo(1_200_000m));
            Assert.That(item.Area, Is.EqualTo(15.0));
            Assert.That(item.DistanceInKm, Is.GreaterThanOrEqualTo(0.0));
            Assert.That(item.IsVerified, Is.True);
            Assert.That(item.Address, Is.Not.Null.And.Not.Empty);
            Assert.That(item.ThumbnailUrl, Is.Not.Null.And.Not.Empty);
            Assert.That(item.RoomType, Is.EqualTo("private"));
            Assert.That(item.Amenities, Does.Contain("wifi"));
        });
    }

    #endregion

    #region Helper Fakes

    private sealed class FakeCampusRepository : ICampusRepository
    {
        private readonly Dictionary<string, Campus> _campuses = [];

        public Task<Campus?> GetByIdAsync(string campusId, CancellationToken cancellationToken = default)
        {
            _campuses.TryGetValue(campusId, out var campus);
            return Task.FromResult(campus);
        }

        public Task AddAsync(Campus campus, CancellationToken cancellationToken = default)
        {
            _campuses[campus.Id] = campus;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _cache = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(key, out var val) && val is T typed)
            {
                return Task.FromResult<T?>(typed);
            }
            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            if (value is not null)
            {
                _cache[key] = value;
            }
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _cache.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRoomListingRepository : IRoomListingRepository
    {
        private readonly List<RoomListing> _items = [];

        public Task<PagedResult<RoomListing>> GetAvailableVerifiedRoomsByCampusAsync(
            string campusId, GeoLocation campusLocation, int page, int pageSize, string sortBy, bool descending, CancellationToken cancellationToken = default)
        {
            var results = _items.Where(r => r.CampusId == campusId && r.Status == ListingRoomStatus.Available && r.VerificationStatus == VerificationStatus.Verified);
            results = sortBy switch
            {
                "price" => descending ? results.OrderByDescending(r => r.MonthlyRent.Amount).ThenBy(r => r.Id) : results.OrderBy(r => r.MonthlyRent.Amount).ThenBy(r => r.Id),
                "area" => descending ? results.OrderByDescending(r => r.AreaSquareMeters).ThenBy(r => r.Id) : results.OrderBy(r => r.AreaSquareMeters).ThenBy(r => r.Id),
                _ => descending ? results.OrderByDescending(r => r.Location.ExactDistanceToKm(campusLocation)).ThenBy(r => r.Id) : results.OrderBy(r => r.Location.ExactDistanceToKm(campusLocation)).ThenBy(r => r.Id)
            };
            var list = results.ToList();
            return Task.FromResult(new PagedResult<RoomListing>(list.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, list.Count));
        }

        public Task AddAsync(RoomListing roomListing, CancellationToken cancellationToken = default)
        {
            _items.Add(roomListing);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string roomId, string campusId, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(item => item.Id == roomId && item.CampusId == campusId);
            return Task.CompletedTask;
        }
    }

    #endregion
}
