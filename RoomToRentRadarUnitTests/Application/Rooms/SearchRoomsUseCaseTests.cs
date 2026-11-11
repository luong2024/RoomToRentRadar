using RoomToRentRadar.Application.UseCases.Rooms;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Rooms;

[TestFixture]
public class SearchRoomsUseCaseTests
{
    private const double IctuLat = 21.5851;
    private const double IctuLng = 105.8066;

    private const string PriceValidationMessage = "MinPrice cannot be greater than MaxPrice or negative.";
    private const string AreaValidationMessage = "MinArea cannot be greater than MaxArea or negative.";
    private const string MaxDistanceValidationMessage = "MaxDistanceKm must be greater than 0.";

    private readonly DateTime _baseCreatedAt = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
    private IRoomRepository _repository = null!;
    private SearchRoomsUseCase _sut = null!;
    private List<Room> _rooms = null!;
    private Room _unverifiedRoom = null!;
    private Room _rentedRoom = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IRoomRepository>();
        _sut = new SearchRoomsUseCase(_repository);

        _rooms = new List<Room>
        {
            NewRoom("Z115", "Phòng trọ Sinh viên Z115", 1_500_000m, 18, 21.5875, 105.8080,
                "Z115, phường Tân Thịnh, Thái Nguyên", createdAt: _baseCreatedAt.AddMinutes(1)),
            NewRoom("TAN-THINH", "Chung cư mini Tân Thịnh", 2_800_000m, 28, 21.5790, 105.8120,
                "Tân Thịnh, Thái Nguyên", createdAt: _baseCreatedAt.AddMinutes(2)),
            NewRoom("THAO-NGUYEN", "Nhà trọ Thảo Nguyên - Quyết Thắng", 1_200_000m, 15, 21.5900, 105.8020,
                "Quyết Thắng, Thái Nguyên", createdAt: _baseCreatedAt.AddMinutes(6)),
            NewRoom("THINH-DAN", "Phòng cao cấp Thịnh Đán", 3_500_000m, 35, 21.5710, 105.8190,
                "Thịnh Đán, Thái Nguyên", createdAt: _baseCreatedAt.AddMinutes(3)),
            NewRoom("KTX-32", "Ký túc xá tư nhân Đường 3/2", 2_000_000m, 22, 21.5600, 105.8250,
                "Đường 3/2, Thái Nguyên", createdAt: _baseCreatedAt.AddMinutes(4)),
            NewRoom("DONG-HY", "Nhà trọ sinh viên giá rẻ Đồng Hỷ", 900_000m, 12, 21.6200, 105.8500,
                "Chợ Điện Đồng Hỷ, Thái Nguyên", createdAt: _baseCreatedAt.AddMinutes(5))
        };

        _unverifiedRoom = NewRoom("UNVERIFIED", "Nhà trọ chưa xác minh (phụ)", 1_300_000m, 16, 21.5860, 105.8070,
            "Tân Thịnh, Thái Nguyên", isVerified: false, createdAt: _baseCreatedAt.AddMinutes(7));
        _rentedRoom = NewRoom("RENTED", "Nhà trọ đã thuê (phụ)", 1_400_000m, 17, 21.5862, 105.8072,
            "Tân Thịnh, Thái Nguyên", isAvailable: false, createdAt: _baseCreatedAt.AddMinutes(8));
        _rooms.Add(_unverifiedRoom);
        _rooms.Add(_rentedRoom);

        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(_rooms);
    }

    private static Room NewRoom(
        string id,
        string title,
        decimal price,
        double area,
        double latitude,
        double longitude,
        string address,
        bool isVerified = true,
        bool isAvailable = true,
        DateTime? createdAt = null)
        => new()
        {
            Id = id,
            Title = title,
            Description = $"{title} - mô tả",
            Price = price,
            Area = area,
            Address = address,
            Latitude = latitude,
            Longitude = longitude,
            LandlordId = "landlord-1",
            IsVerified = isVerified,
            IsAvailable = isAvailable,
            CreatedAtUtc = createdAt ?? DateTime.UtcNow
        };

    private static string[] Ids(params string[] ids) => ids;

    // ---------------------------------------------------------------- Lọc giá

    [Test]
    public async Task ExecuteAsync_WithPriceRange_ReturnsOnlyRoomsWithinPriceRange()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(MinPrice: 1_000_000m, MaxPrice: 2_000_000m));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(i => i.Id), Is.EquivalentTo(Ids("Z115", "THAO-NGUYEN", "KTX-32")));
            Assert.That(result.TotalCount, Is.EqualTo(3));
            Assert.That(result.TotalCount, Is.LessThan(_rooms.Count), "Phòng phụ (chưa xác minh / đã thuê) phải bị loại");
        });
    }

    [Test]
    public async Task ExecuteAsync_PriceRangeIsInclusiveOnBothBounds()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(MinPrice: 900_000m, MaxPrice: 1_500_000m));

        Assert.That(result.Items.Select(i => i.Id), Is.EquivalentTo(Ids("Z115", "THAO-NGUYEN", "DONG-HY")));
    }

    // ---------------------------------------------------------- Lọc diện tích

    [Test]
    public async Task ExecuteAsync_WithAreaRange_ReturnsOnlyRoomsWithinAreaRange()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(MinArea: 20, MaxArea: 30));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(i => i.Id), Is.EquivalentTo(Ids("TAN-THINH", "KTX-32")));
            Assert.That(result.TotalCount, Is.EqualTo(2));
        });
    }

    // --------------------------------------------------------- Lọc khoảng cách

    [Test]
    public async Task ExecuteAsync_WithMaxDistance_ExcludesRoomsFartherThanLimit()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(MaxDistanceKm: 1.5));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(i => i.Id),
                Is.EquivalentTo(Ids("Z115", "TAN-THINH", "THAO-NGUYEN")));
            Assert.That(result.Items.Select(i => i.Id), Has.None.EqualTo("THINH-DAN"));
            Assert.That(result.Items.Select(i => i.Id), Has.None.EqualTo("KTX-32"));
            Assert.That(result.Items.Select(i => i.Id), Has.None.EqualTo("DONG-HY"));
            Assert.That(result.Items.All(i => i.DistanceKm <= 1.5), Is.True);
        });
    }

    // ---------------------------------------------------------- Kết hợp bộ lọc

    [Test]
    public async Task ExecuteAsync_WithPriceAreaAndDistanceCombined_ReturnsIntersection()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(
            MinPrice: 1_000_000m, MaxPrice: 2_000_000m,
            MinArea: 20, MaxArea: 30,
            MaxDistanceKm: 1.5));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(i => i.Id), Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(0));
        });
    }

    // ------------------------------------------------ Verified + Available

    [Test]
    public async Task ExecuteAsync_AlwaysExcludesUnverifiedAndUnavailableRooms()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(i => i.Id), Has.None.EqualTo("UNVERIFIED"));
            Assert.That(result.Items.Select(i => i.Id), Has.None.EqualTo("RENTED"));
            Assert.That(result.Items.Select(i => i.Id),
                Is.EquivalentTo(Ids("Z115", "TAN-THINH", "THAO-NGUYEN", "THINH-DAN", "KTX-32", "DONG-HY")));
            Assert.That(result.TotalCount, Is.EqualTo(6));
            Assert.That(result.Items.All(i => i.IsVerified && i.IsAvailable), Is.True);
        });
    }

    [Test]
    public async Task ExecuteAsync_UnverifiedAndUnavailableRooms_StillCountInRepositoryButNeverInResult()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Room> { _unverifiedRoom, _rentedRoom });

        var result = await _sut.ExecuteAsync(new RoomSearchRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(0));
        });
    }

    // ------------------------------------------------------------- Validation

    private static IEnumerable<TestCaseData> InvalidRequests()
    {
        yield return new TestCaseData(new RoomSearchRequest(MinPrice: 2_000_000m, MaxPrice: 1_000_000m), PriceValidationMessage)
            .SetName("Validate_MinPriceGreaterThanMaxPrice_Throws");
        yield return new TestCaseData(new RoomSearchRequest(MinPrice: -1m), PriceValidationMessage)
            .SetName("Validate_NegativeMinPrice_Throws");
        yield return new TestCaseData(new RoomSearchRequest(MaxPrice: -1m), PriceValidationMessage)
            .SetName("Validate_NegativeMaxPrice_Throws");
        yield return new TestCaseData(new RoomSearchRequest(MinArea: 30, MaxArea: 20), AreaValidationMessage)
            .SetName("Validate_MinAreaGreaterThanMaxArea_Throws");
        yield return new TestCaseData(new RoomSearchRequest(MinArea: -1), AreaValidationMessage)
            .SetName("Validate_NegativeMinArea_Throws");
        yield return new TestCaseData(new RoomSearchRequest(MaxArea: -1), AreaValidationMessage)
            .SetName("Validate_NegativeMaxArea_Throws");
        yield return new TestCaseData(new RoomSearchRequest(MaxDistanceKm: 0), MaxDistanceValidationMessage)
            .SetName("Validate_ZeroMaxDistance_Throws");
        yield return new TestCaseData(new RoomSearchRequest(MaxDistanceKm: -2), MaxDistanceValidationMessage)
            .SetName("Validate_NegativeMaxDistance_Throws");
    }

    [TestCaseSource(nameof(InvalidRequests))]
    public async Task ExecuteAsync_InvalidRequest_ThrowsDomainValidationExceptionWithoutCallingRepository(
        RoomSearchRequest request, string expectedMessage)
    {
        var exception = Assert.ThrowsAsync<DomainValidationException>(async () => await _sut.ExecuteAsync(request));

        Assert.That(exception!.Message, Is.EqualTo(expectedMessage));
        await _repository.DidNotReceive().GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_ZeroDistanceBounds_AreAllowed()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(MinPrice: 0m, MaxPrice: 0m, MinArea: 0, MaxArea: 0));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(0));
        });
    }

    // --------------------------------------------------------------- Sắp xếp

    [Test]
    public async Task ExecuteAsync_DefaultSort_OrdersByDistanceAscending()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(PageSize: 50));

        var distances = result.Items.Select(i => i.DistanceKm).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(distances, Is.Ordered.Ascending);
            Assert.That(result.Items[0].Id, Is.EqualTo("Z115"));
            Assert.That(result.Items[^1].Id, Is.EqualTo("DONG-HY"));
        });
    }

    [Test]
    public async Task ExecuteAsync_SortByDistanceExplicit_OrdersByDistanceAscending()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(SortBy: "distance", PageSize: 50));

        Assert.That(result.Items.Select(i => i.DistanceKm), Is.Ordered.Ascending);
    }

    [Test]
    public async Task ExecuteAsync_SortByPriceAscending_OrdersByPriceAscending()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(SortBy: "price_asc", PageSize: 50));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(i => i.Price), Is.Ordered.Ascending);
            Assert.That(result.Items[0].Id, Is.EqualTo("DONG-HY"));
            Assert.That(result.Items[^1].Id, Is.EqualTo("THINH-DAN"));
        });
    }

    [Test]
    public async Task ExecuteAsync_SortByPriceDescending_OrdersByPriceDescending()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(SortBy: "price_desc", PageSize: 50));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(i => i.Price), Is.Ordered.Descending);
            Assert.That(result.Items[0].Id, Is.EqualTo("THINH-DAN"));
            Assert.That(result.Items[^1].Id, Is.EqualTo("DONG-HY"));
        });
    }

    [Test]
    public async Task ExecuteAsync_SortByAreaAscending_OrdersByAreaAscending()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(SortBy: "area_asc", PageSize: 50));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(i => i.Area), Is.Ordered.Ascending);
            Assert.That(result.Items[0].Id, Is.EqualTo("DONG-HY"));
            Assert.That(result.Items[^1].Id, Is.EqualTo("THINH-DAN"));
        });
    }

    [Test]
    public async Task ExecuteAsync_SortByAreaDescending_OrdersByAreaDescending()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(SortBy: "area_desc", PageSize: 50));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(i => i.Area), Is.Ordered.Descending);
            Assert.That(result.Items[0].Id, Is.EqualTo("THINH-DAN"));
            Assert.That(result.Items[^1].Id, Is.EqualTo("DONG-HY"));
        });
    }

    [Test]
    public async Task ExecuteAsync_SortByNewest_OrdersByCreatedAtDescending()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(SortBy: "newest", PageSize: 50));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(i => i.CreatedAtUtc), Is.Ordered.Descending);
            Assert.That(result.Items[0].Id, Is.EqualTo("THAO-NGUYEN"));
            Assert.That(result.Items[^1].Id, Is.EqualTo("Z115"));
        });
    }

    [Test]
    public async Task ExecuteAsync_SortByIsCaseInsensitive()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(SortBy: "PRICE_ASC", PageSize: 50));

        Assert.That(result.Items.Select(i => i.Price), Is.Ordered.Ascending);
    }

    [Test]
    public async Task ExecuteAsync_UnknownSortBy_FallsBackToDistance()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(SortBy: "rating_desc", PageSize: 50));

        Assert.That(result.Items.Select(i => i.DistanceKm), Is.Ordered.Ascending);
    }

    // ------------------------------------------------------------- Phân trang

    [Test]
    public async Task ExecuteAsync_FirstPage_ReturnsRequestedPageSizeAndTotalCountBeforePaging()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(SortBy: "price_asc", Page: 1, PageSize: 2));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(i => i.Id), Is.EqualTo(Ids("DONG-HY", "THAO-NGUYEN")));
            Assert.That(result.TotalCount, Is.EqualTo(6), "TotalCount = số phần tử sau lọc, trước phân trang");
            Assert.That(result.PageNumber, Is.EqualTo(1));
            Assert.That(result.PageSize, Is.EqualTo(2));
            Assert.That(result.TotalPages, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task ExecuteAsync_SecondPage_ReturnsNextSlice()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(SortBy: "price_asc", Page: 2, PageSize: 2));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(i => i.Id), Is.EqualTo(Ids("Z115", "KTX-32")));
            Assert.That(result.TotalCount, Is.EqualTo(6));
            Assert.That(result.PageNumber, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ExecuteAsync_PageBeyondLast_ReturnsEmptyItemsButKeepsTotalCount()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(Page: 10, PageSize: 2));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(6));
        });
    }

    [Test]
    public async Task ExecuteAsync_PageIntMaxValue_ReturnsEmptyItemsInsteadOfOverflowingToFirstPage()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(Page: int.MaxValue, PageSize: 10));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Is.Empty, "(page - 1) * pageSize tràn int không được kéo Skip về 0");
            Assert.That(result.TotalCount, Is.EqualTo(6));
            Assert.That(result.PageNumber, Is.EqualTo(int.MaxValue));
            Assert.That(result.PageSize, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task ExecuteAsync_PageThatOverflowsIntWhenMultipliedByPageSize_ReturnsEmptyItems()
    {
        // 536870912 * 10 = 5_368_709_120 > int.MaxValue -> Skip(int) cũ tràn thành số âm.
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(Page: 536_870_912, PageSize: 10));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(6));
        });
    }

    [Test]
    public async Task ExecuteAsync_PageAndPageSizeBelowOne_AreClampedToOne()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(Page: 0, PageSize: 0));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.PageNumber, Is.EqualTo(1));
            Assert.That(result.PageSize, Is.EqualTo(1));
            Assert.That(result.TotalCount, Is.EqualTo(6));
        });
    }

    // --------------------------------------------------------- Tọa độ gốc tùy chỉnh

    [Test]
    public async Task ExecuteAsync_CustomOrigin_RecalculatesDistancesAndDistanceSortOrder()
    {
        // Gốc tùy chỉnh ngay tại Thịnh Đán (21.5710, 105.8190)
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(
            Latitude: 21.5710, Longitude: 105.8190, SortBy: "distance", PageSize: 50));

        var thinhDan = result.Items.Single(i => i.Id == "THINH-DAN");
        var z115 = result.Items.Single(i => i.Id == "Z115");

        Assert.Multiple(() =>
        {
            Assert.That(thinhDan.DistanceKm, Is.EqualTo(0.0));
            Assert.That(z115.DistanceKm,
                Is.EqualTo(HaversineDistanceCalculator.CalculateDistanceKm(21.5710, 105.8190, 21.5875, 105.8080)));
            Assert.That(z115.DistanceKm, Is.GreaterThan(1.5));
            Assert.That(result.Items[0].Id, Is.EqualTo("THINH-DAN"), "Phòng ở gốc phải đứng đầu khi sắp xếp theo khoảng cách");
        });
    }

    [Test]
    public async Task ExecuteAsync_CustomOrigin_AppliesMaxDistanceAgainstCustomOrigin()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(
            Latitude: 21.5710, Longitude: 105.8190, MaxDistanceKm: 0.1));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(i => i.Id), Is.EqualTo(Ids("THINH-DAN")));
            Assert.That(result.TotalCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ExecuteAsync_OnlyLatitudeProvided_UsesIctuLongitudeDefault()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(Latitude: 21.5710, SortBy: "distance", PageSize: 50));

        var z115 = result.Items.Single(i => i.Id == "Z115");
        Assert.That(z115.DistanceKm,
            Is.EqualTo(HaversineDistanceCalculator.CalculateDistanceKm(21.5710, IctuLng, 21.5875, 105.8080)));
    }

    // ---------------------------------------------------------------- Keyword

    [Test]
    public async Task ExecuteAsync_KeywordWithoutDiacritics_MatchesVietnameseTitleAndAddress()
    {
        var byTitle = await _sut.ExecuteAsync(new RoomSearchRequest(Keyword: "dien", PageSize: 50));
        var byAddress = await _sut.ExecuteAsync(new RoomSearchRequest(Keyword: "duong 3/2", PageSize: 50));

        Assert.Multiple(() =>
        {
            Assert.That(byTitle.Items.Select(i => i.Id), Is.EqualTo(Ids("DONG-HY")), "\"dien\" phải khớp \"Điện\", không khớp \"Đường\"");
            Assert.That(byAddress.Items.Select(i => i.Id), Is.EqualTo(Ids("KTX-32")), "\"duong\" phải khớp \"Đường\"");
        });
    }

    [Test]
    public async Task ExecuteAsync_KeywordWithDiacritics_MatchesTitle()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(Keyword: "Điện"));

        Assert.That(result.Items.Select(i => i.Id), Is.EqualTo(Ids("DONG-HY")));
    }

    [Test]
    public async Task ExecuteAsync_KeywordIsCaseInsensitiveAndMatchesRoomCode()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(Keyword: "z115"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(i => i.Id), Is.EqualTo(Ids("Z115")));
            Assert.That(result.TotalCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ExecuteAsync_DiacriticKeyword_MatchesDiacriticTitleToo()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(Keyword: "Phòng"));

        Assert.That(result.Items.Select(i => i.Id), Has.Member("Z115"));
    }

    [Test]
    public async Task ExecuteAsync_KeywordMatchesOnlyFilteredRooms_NeverUnverified()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(Keyword: "chua xac minh"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task ExecuteAsync_WhitespaceKeyword_IsIgnored()
    {
        var blank = await _sut.ExecuteAsync(new RoomSearchRequest(Keyword: "   "));
        var none = await _sut.ExecuteAsync(new RoomSearchRequest());

        Assert.That(blank.TotalCount, Is.EqualTo(none.TotalCount));
    }

    // ---------------------------------------------------------------- Mapping

    [Test]
    public async Task ExecuteAsync_MapsAllRoomCardFieldsAndHaversineDistance()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(Keyword: "z115"));
        var source = _rooms.Single(r => r.Id == "Z115");
        var dto = result.Items.Single();

        Assert.Multiple(() =>
        {
            Assert.That(dto.Id, Is.EqualTo(source.Id));
            Assert.That(dto.Title, Is.EqualTo(source.Title));
            Assert.That(dto.Description, Is.EqualTo(source.Description));
            Assert.That(dto.Price, Is.EqualTo(source.Price));
            Assert.That(dto.Area, Is.EqualTo(source.Area));
            Assert.That(dto.Address, Is.EqualTo(source.Address));
            Assert.That(dto.Latitude, Is.EqualTo(source.Latitude));
            Assert.That(dto.Longitude, Is.EqualTo(source.Longitude));
            Assert.That(dto.DistanceKm,
                Is.EqualTo(HaversineDistanceCalculator.CalculateDistanceKm(IctuLat, IctuLng, source.Latitude, source.Longitude)));
            Assert.That(dto.DistanceKm, Is.EqualTo(0.30).Within(0.01));
            Assert.That(dto.IsAvailable, Is.True);
            Assert.That(dto.IsVerified, Is.True);
            Assert.That(dto.CreatedAtUtc, Is.EqualTo(source.CreatedAtUtc));
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.PageNumber, Is.EqualTo(1));
            Assert.That(result.PageSize, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenNoRoomsInRepository_ReturnsEmptyPagedResult()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Room>());

        var result = await _sut.ExecuteAsync(new RoomSearchRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(0));
            Assert.That(result.PageNumber, Is.EqualTo(1));
            Assert.That(result.PageSize, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task ExecuteAsync_ForwardsCancellationTokenToRepository()
    {
        using var cts = new CancellationTokenSource();

        await _sut.ExecuteAsync(new RoomSearchRequest(), cts.Token);

        await _repository.Received(1).GetAllAsync(cts.Token);
    }

    [Test]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SearchRoomsUseCase(null!));
    }
}
