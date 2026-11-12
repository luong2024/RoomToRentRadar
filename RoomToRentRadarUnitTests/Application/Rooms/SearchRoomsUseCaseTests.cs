using RoomToRentRadar.Application.UseCases.Rooms;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Rooms;

/// <summary>
/// UC-05 (Search and Filter Listings) / <c>operationId: searchRooms</c> / BR-SRCH-01..06, BR-CMP-01..03.
/// RED ở Task 1: <see cref="SearchRoomsUseCase"/> chỉ có stub ném <see cref="NotImplementedException"/>;
/// Task 2 (Giai đoạn 2) làm xanh toàn bộ fixture này mà không cần sửa test.
/// </summary>
[TestFixture]
public class SearchRoomsUseCaseTests
{
    private static readonly DateTime BasePublishedAt = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Campus Ictu = new(
        "ICTU", "Trường Đại học Công nghệ Thông tin và Truyền thông", "ICTU", 21.5851, 105.8066, isDefault: true, sortOrder: 1);
    private static readonly Campus Tnut = new(
        "TNUT", "Trường Đại học Kỹ thuật Công nghiệp", "TNUT", 21.54954, 105.84350, isDefault: false, sortOrder: 8);

    private ICampusRepository _campusRepository = null!;
    private IRoomSearchReadModel _readModel = null!;
    private SearchRoomsUseCase _sut = null!;
    private RoomSearchQueryFilter? _capturedFilter;

    /// <summary>
    /// 5 phòng Available quanh ICTU (cùng dữ liệu tinh thần với seed Task 3) + 1 phòng BankHouse
    /// ở TNUT (xa) để kiểm lọc khoảng cách và campus.
    /// </summary>
    private static readonly RoomSearchCandidate[] Candidates =
    {
        NewCandidate("cand-z115", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 18.0, 21.5875, 105.8080, BasePublishedAt.AddMinutes(1)),
        NewCandidate("cand-tan-thinh", "Chung cư mini Tân Thịnh", RoomType.MiniApartment, 2_800_000L, 28.0, 21.5790, 105.8120, BasePublishedAt.AddMinutes(2)),
        NewCandidate("cand-thao-nguyen", "Nhà trọ Thảo Nguyên", RoomType.BoardingRoom, 1_200_000L, 15.0, 21.5900, 105.8020, BasePublishedAt.AddMinutes(3)),
        NewCandidate("cand-thinh-dan", "Phòng cao cấp Thịnh Đán", RoomType.MiniApartment, 3_500_000L, 35.0, 21.5710, 105.8190, BasePublishedAt.AddMinutes(4)),
        NewCandidate("cand-ktx-32", "Ký túc xá tư nhân Đường 3/2", RoomType.Dormitory, 2_000_000L, 22.0, 21.5600, 105.8250, BasePublishedAt.AddMinutes(5)),
        NewCandidate("cand-tnut-house", "Nhà nguyên căn gần TNUT", RoomType.WholeHouse, 4_200_000L, 45.0, 21.54954, 105.84350, BasePublishedAt.AddMinutes(6))
    };

    [SetUp]
    public void SetUp()
    {
        _campusRepository = Substitute.For<ICampusRepository>();
        _readModel = Substitute.For<IRoomSearchReadModel>();
        _sut = new SearchRoomsUseCase(_campusRepository, _readModel);
        _capturedFilter = null;

        _campusRepository.GetDefaultCampusAsync(Arg.Any<CancellationToken>()).Returns(Ictu);
        _campusRepository.GetByIdAsync("ICTU", Arg.Any<CancellationToken>()).Returns(Ictu);
        _campusRepository.GetByIdAsync("TNUT", Arg.Any<CancellationToken>()).Returns(Tnut);
        _campusRepository.GetByIdAsync(Arg.Is<string>(id => id != "ICTU" && id != "TNUT"), Arg.Any<CancellationToken>())
            .Returns((Campus?)null);

        _readModel
            .QueryCandidatesAsync(Arg.Any<RoomSearchQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                _capturedFilter = callInfo.Arg<RoomSearchQueryFilter>();
                return Task.FromResult<IReadOnlyList<RoomSearchCandidate>>(Candidates);
            });
    }

    private static RoomSearchCandidate NewCandidate(
        string id,
        string title,
        RoomType roomType,
        long monthlyRentVnd,
        double areaM2,
        double latitude,
        double longitude,
        DateTime? publishedAtUtc) => new(
            Id: id,
            Title: title,
            MonthlyRentVnd: monthlyRentVnd,
            AreaM2: areaM2,
            RoomType: roomType,
            Status: ListingStatus.Available,
            IsVerified: true,
            CoverThumbUrl: null,
            Latitude: latitude,
            Longitude: longitude,
            PublishedAtUtc: publishedAtUtc);

    // ----------------------------------------------------------------- validation

    [TestCase("area_asc")]
    [TestCase("price")]
    [TestCase("DISTANCE")]
    [TestCase("")]
    public void ExecuteAsync_UnknownSort_ThrowsDomainValidationExceptionWithInvalidSortOption(string sort)
    {
        var request = new RoomSearchRequest(Sort: sort);

        var exception = Assert.ThrowsAsync<DomainValidationException>(
            async () => await _sut.ExecuteAsync(request));

        Assert.That(exception!.ErrorCode, Is.EqualTo("INVALID_SORT_OPTION")); // BR-SRCH-02
    }

    [Test]
    public async Task ExecuteAsync_UnknownSort_DoesNotQueryRepository()
    {
        var request = new RoomSearchRequest(Sort: "area_desc");

        Assert.ThrowsAsync<DomainValidationException>(async () => await _sut.ExecuteAsync(request));

        await _readModel.DidNotReceiveWithAnyArgs().QueryCandidatesAsync(default!);
        await _campusRepository.DidNotReceiveWithAnyArgs().GetDefaultCampusAsync();
    }

    [Test]
    public void ExecuteAsync_MinRentGreaterThanMaxRent_ThrowsInvalidPriceRange()
    {
        var request = new RoomSearchRequest(MinRentVnd: 3_000_000L, MaxRentVnd: 1_000_000L);

        var exception = Assert.ThrowsAsync<DomainValidationException>(async () => await _sut.ExecuteAsync(request));

        Assert.That(exception!.ErrorCode, Is.EqualTo("INVALID_PRICE_RANGE"));
    }

    [Test]
    public void ExecuteAsync_NegativeRent_Throws()
    {
        var request = new RoomSearchRequest(MinRentVnd: -1);

        Assert.ThrowsAsync<DomainValidationException>(async () => await _sut.ExecuteAsync(request));
    }

    [Test]
    public void ExecuteAsync_MinAreaGreaterThanMaxArea_ThrowsInvalidAreaRange()
    {
        var request = new RoomSearchRequest(MinAreaM2: 40, MaxAreaM2: 10);

        var exception = Assert.ThrowsAsync<DomainValidationException>(async () => await _sut.ExecuteAsync(request));

        Assert.That(exception!.ErrorCode, Is.EqualTo("INVALID_AREA_RANGE"));
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void ExecuteAsync_NonFiniteAreaFilter_Throws(double area)
    {
        Assert.Multiple(() =>
        {
            Assert.ThrowsAsync<DomainValidationException>(
                async () => await _sut.ExecuteAsync(new RoomSearchRequest(MaxAreaM2: area)));
            Assert.ThrowsAsync<DomainValidationException>(
                async () => await _sut.ExecuteAsync(new RoomSearchRequest(MinAreaM2: area)));
        });
    }

    [TestCase(0.0)]
    [TestCase(0.05)]
    [TestCase(50.1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void ExecuteAsync_MaxDistanceOutOfRange_Throws(double maxDistanceKm)
    {
        Assert.ThrowsAsync<DomainValidationException>(
            async () => await _sut.ExecuteAsync(new RoomSearchRequest(MaxDistanceKm: maxDistanceKm)));
    }

    // ----------------------------------------------------------------- campus (BR-CMP-01..03)

    [Test]
    public void ExecuteAsync_UnknownCampusId_ThrowsCampusNotFound()
    {
        var request = new RoomSearchRequest(CampusId: "NOPE");

        var exception = Assert.ThrowsAsync<DomainValidationException>(async () => await _sut.ExecuteAsync(request));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.ErrorCode, Is.EqualTo("CAMPUS_NOT_FOUND"));
            Assert.That(exception.Message, Does.Contain("NOPE"));
        });
    }

    [Test]
    public async Task ExecuteAsync_WithoutCampusId_UsesDefaultCampusIctu()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest());

        await _campusRepository.Received(1).GetDefaultCampusAsync(Arg.Any<CancellationToken>());
        Assert.That(result.Items, Is.Not.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WithCampusId_UsesThatCampusAsDistanceOrigin()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(CampusId: "TNUT"));

        var nearest = result.Items[0];
        Assert.That(nearest.Id, Is.EqualTo("cand-tnut-house"), "Sắp xếp mặc định 'distance' tính từ TNUT");
    }

    // ----------------------------------------------------------------- read model query (M2 / ADR-006)

    [Test]
    public async Task ExecuteAsync_WithoutFilters_PassesEmptyFilterWithNullBoundingBox()
    {
        await _sut.ExecuteAsync(new RoomSearchRequest());

        Assert.That(_capturedFilter, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(_capturedFilter!.MinRentVnd, Is.Null);
            Assert.That(_capturedFilter.MaxRentVnd, Is.Null);
            Assert.That(_capturedFilter.MinAreaM2, Is.Null);
            Assert.That(_capturedFilter.MaxAreaM2, Is.Null);
            Assert.That(_capturedFilter.RoomTypes, Is.Null);
            Assert.That(_capturedFilter.MinLat, Is.Null);
            Assert.That(_capturedFilter.MaxLat, Is.Null);
            Assert.That(_capturedFilter.MinLng, Is.Null);
            Assert.That(_capturedFilter.MaxLng, Is.Null);
        });
    }

    [Test]
    public async Task ExecuteAsync_WithFilters_PushesPriceAreaRoomTypesToReadModel()
    {
        var request = new RoomSearchRequest(
            MinRentVnd: 1_000_000L,
            MaxRentVnd: 3_000_000L,
            MinAreaM2: 15,
            MaxAreaM2: 30,
            RoomTypes: new[] { RoomType.BoardingRoom, RoomType.Dormitory });

        await _sut.ExecuteAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(_capturedFilter!.MinRentVnd, Is.EqualTo(1_000_000L));
            Assert.That(_capturedFilter.MaxRentVnd, Is.EqualTo(3_000_000L));
            Assert.That(_capturedFilter.MinAreaM2, Is.EqualTo(15));
            Assert.That(_capturedFilter.MaxAreaM2, Is.EqualTo(30));
            Assert.That(_capturedFilter.RoomTypes, Is.EquivalentTo(new[] { RoomType.BoardingRoom, RoomType.Dormitory }));
        });
    }

    [Test]
    public async Task ExecuteAsync_WithMaxDistance_ComputesBoundingBoxAroundCampus()
    {
        // ADR-006: deltaLat = km / 110.574; deltaLng = km / (111.320 * cos(lat))
        await _sut.ExecuteAsync(new RoomSearchRequest(MaxDistanceKm: 2.0));

        var deltaLat = 2.0 / 110.574;
        var deltaLng = 2.0 / (111.320 * Math.Cos(21.5851 * Math.PI / 180.0));

        Assert.That(_capturedFilter, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(_capturedFilter!.MinLat, Is.EqualTo(21.5851 - deltaLat).Within(1e-9));
            Assert.That(_capturedFilter.MaxLat, Is.EqualTo(21.5851 + deltaLat).Within(1e-9));
            Assert.That(_capturedFilter.MinLng, Is.EqualTo(105.8066 - deltaLng).Within(1e-9));
            Assert.That(_capturedFilter.MaxLng, Is.EqualTo(105.8066 + deltaLng).Within(1e-9));
        });
    }

    // ----------------------------------------------------------------- filtering (AC1/AC2)

    [Test]
    public async Task ExecuteAsync_WithRoomTypes_ReturnsOnlySelectedTypes()
    {
        var request = new RoomSearchRequest(RoomTypes: new[] { RoomType.MiniApartment });

        var result = await _sut.ExecuteAsync(request);

        Assert.That(result.Items, Is.Not.Empty);
        Assert.That(result.Items.Select(item => item.RoomType), Is.All.EqualTo(RoomType.MiniApartment));
    }

    [Test]
    public async Task ExecuteAsync_WithMaxDistance_KeepsOnlyRoomsWithinExactHaversineDistance()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(MaxDistanceKm: 2.0));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Is.Not.Empty);
            Assert.That(result.Items.Select(item => item.Id), Does.Not.Contain("cand-tnut-house"));
            foreach (var item in result.Items)
            {
                var expected = HaversineDistanceCalculator.CalculateDistanceKm(
                    Ictu.Latitude, Ictu.Longitude, item.Latitude, item.Longitude);
                Assert.That(item.DistanceKm, Is.EqualTo(expected).Within(0.005));
                Assert.That(item.DistanceKm, Is.LessThanOrEqualTo(2.0));
            }
        });
    }

    [Test]
    public async Task ExecuteAsync_ReturnsOnlyAvailableRooms_AsReportedByReadModel()
    {
        _readModel
            .QueryCandidatesAsync(Arg.Any<RoomSearchQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                NewCandidate("cand-available", "Phòng còn trống", RoomType.BoardingRoom, 1_500_000L, 18, 21.5875, 105.8080, BasePublishedAt),
                NewCandidate("cand-other", "Phòng khác", RoomType.BoardingRoom, 1_600_000L, 19, 21.5880, 105.8085, BasePublishedAt)
                    with { Status = ListingStatus.Rented }
            });

        var result = await _sut.ExecuteAsync(new RoomSearchRequest());

        Assert.That(result.Items.Select(item => item.Id), Is.EquivalentTo(new[] { "cand-available" }));
    }

    // ----------------------------------------------------------------- sorting (BR-SRCH-04)

    [Test]
    public async Task ExecuteAsync_DefaultSort_OrdersByDistanceFromCampus()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest());

        var distances = result.Items.Select(item => item.DistanceKm).ToList();
        Assert.That(distances, Is.Ordered, "Mặc định sort = 'distance' (BR-SRCH-04)");
    }

    [Test]
    public async Task ExecuteAsync_SortPriceAsc_OrdersByMonthlyRentAscending()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(Sort: "price_asc"));

        Assert.That(result.Items.Select(item => item.MonthlyRentVnd), Is.Ordered);
    }

    [Test]
    public async Task ExecuteAsync_SortPriceDesc_OrdersByMonthlyRentDescending()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(Sort: "price_desc"));

        Assert.That(result.Items.Select(item => item.MonthlyRentVnd), Is.Ordered.Descending);
    }

    [Test]
    public async Task ExecuteAsync_SortNewest_OrdersByPublishedAtDescending()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(Sort: "newest"));

        var publishedDates = result.Items.Select(item => item.PublishedAtUtc ?? DateTime.MinValue).ToList();
        Assert.That(publishedDates, Is.Ordered.Descending);
        Assert.That(result.Items[0].Id, Is.EqualTo("cand-tnut-house"));
    }

    [Test]
    public async Task ExecuteAsync_SortNewest_NullPublishedAtGoesLast()
    {
        _readModel
            .QueryCandidatesAsync(Arg.Any<RoomSearchQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                NewCandidate("cand-null", "Chưa publish", RoomType.BoardingRoom, 1_500_000L, 18, 21.5875, 105.8080, null),
                NewCandidate("cand-old", "Publish cũ", RoomType.BoardingRoom, 1_500_000L, 18, 21.5876, 105.8081, BasePublishedAt)
            });

        var result = await _sut.ExecuteAsync(new RoomSearchRequest(Sort: "newest"));

        Assert.That(result.Items.Select(item => item.Id), Is.EqualTo(new[] { "cand-old", "cand-null" }));
    }

    [Test]
    public async Task ExecuteAsync_EqualValues_AreTiedByRoomId()
    {
        _readModel
            .QueryCandidatesAsync(Arg.Any<RoomSearchQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                NewCandidate("cand-b", "Phòng B", RoomType.BoardingRoom, 1_500_000L, 18, 21.5875, 105.8080, BasePublishedAt),
                NewCandidate("cand-a", "Phòng A", RoomType.BoardingRoom, 1_500_000L, 18, 21.5875, 105.8080, BasePublishedAt)
            });

        var result = await _sut.ExecuteAsync(new RoomSearchRequest(Sort: "price_asc"));

        Assert.That(result.Items.Select(item => item.Id), Is.EqualTo(new[] { "cand-a", "cand-b" }));
    }

    [Test]
    public async Task ExecuteAsync_SortIsCaseInsensitiveAndTrimmed()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(Sort: "  PRICE_DESC  "));

        Assert.That(result.Items.Select(item => item.MonthlyRentVnd), Is.Ordered.Descending);
    }

    // ----------------------------------------------------------------- paging (US04 AC3)

    [Test]
    public async Task ExecuteAsync_FirstPage_ReturnsRequestedPageSizeAndTotalCount()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(PageNumber: 1, PageSize: 2));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Has.Count.EqualTo(2));
            Assert.That(result.TotalCount, Is.EqualTo(Candidates.Length));
            Assert.That(result.PageNumber, Is.EqualTo(1));
            Assert.That(result.PageSize, Is.EqualTo(2));
            Assert.That(result.TotalPages, Is.EqualTo(3));
            Assert.That(result.HasNextPage, Is.True);
            Assert.That(result.HasPreviousPage, Is.False);
        });
    }

    [Test]
    public async Task ExecuteAsync_SecondPage_ReturnsNextSliceWithoutOverlap()
    {
        var first = await _sut.ExecuteAsync(new RoomSearchRequest(PageNumber: 1, PageSize: 2));
        var second = await _sut.ExecuteAsync(new RoomSearchRequest(PageNumber: 2, PageSize: 2));

        Assert.Multiple(() =>
        {
            Assert.That(second.Items, Has.Count.EqualTo(2));
            Assert.That(second.Items.Select(item => item.Id).Intersect(first.Items.Select(item => item.Id)), Is.Empty);
        });
    }

    [Test]
    public async Task ExecuteAsync_PageBeyondLast_ReturnsEmptyItemsButKeepsTotalCount()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(PageNumber: 99, PageSize: 20));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(Candidates.Length));
        });
    }

    [Test]
    public async Task ExecuteAsync_PageSizeAboveMax_IsClampedTo50AndPageBelowOneToOne()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(PageNumber: 0, PageSize: 500));

        Assert.Multiple(() =>
        {
            Assert.That(result.PageSize, Is.EqualTo(RoomSearchRequest.MaxPageSize));
            Assert.That(result.PageNumber, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ExecuteAsync_HugePageNumber_DoesNotOverflow()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(PageNumber: int.MaxValue, PageSize: 50));

        Assert.That(result.Items, Is.Empty);
    }

    // ----------------------------------------------------------------- mapping / robustness

    [Test]
    public async Task ExecuteAsync_MapsRoomCardDtoAndMarksAnonymousAsNotFavourite()
    {
        var result = await _sut.ExecuteAsync(new RoomSearchRequest(RoomTypes: new[] { RoomType.WholeHouse }));

        var card = result.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(card.Id, Is.EqualTo("cand-tnut-house"));
            Assert.That(card.Title, Is.EqualTo("Nhà nguyên căn gần TNUT"));
            Assert.That(card.MonthlyRentVnd, Is.EqualTo(4_200_000L));
            Assert.That(card.AreaM2, Is.EqualTo(45.0));
            Assert.That(card.RoomType, Is.EqualTo(RoomType.WholeHouse));
            Assert.That(card.IsVerified, Is.True);
            Assert.That(card.Status, Is.EqualTo(ListingStatus.Available));
            Assert.That(card.Latitude, Is.EqualTo(21.54954));
            Assert.That(card.Longitude, Is.EqualTo(105.84350));
            Assert.That(card.PublishedAtUtc, Is.EqualTo(BasePublishedAt.AddMinutes(6)));
            Assert.That(card.IsFavourite, Is.False);
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenNoCandidates_ReturnsEmptyPagedResult()
    {
        _readModel
            .QueryCandidatesAsync(Arg.Any<RoomSearchQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RoomSearchCandidate>());

        var result = await _sut.ExecuteAsync(new RoomSearchRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Is.Empty);
            Assert.That(result.TotalCount, Is.Zero);
            Assert.That(result.TotalPages, Is.Zero);
        });
    }

    [Test]
    public async Task ExecuteAsync_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        await _sut.ExecuteAsync(new RoomSearchRequest(), cts.Token);

        await _campusRepository.Received(1).GetDefaultCampusAsync(cts.Token);
        await _readModel.Received(1).QueryCandidatesAsync(Arg.Any<RoomSearchQueryFilter>(), cts.Token);
    }

    [Test]
    public void Constructor_WithNullDependency_ThrowsArgumentNullException()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => new SearchRoomsUseCase(null!, _readModel));
            Assert.Throws<ArgumentNullException>(() => new SearchRoomsUseCase(_campusRepository, null!));
        });
    }
}
