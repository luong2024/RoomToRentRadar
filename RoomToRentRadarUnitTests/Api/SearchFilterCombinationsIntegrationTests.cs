using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadarUnitTests.Search.Fixtures;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// US04 / T04.3 group 1 — filter combinations, inclusive boundaries, sorting with tie-breaks, campus
/// changes, paging consistency and clear-filters over the real HTTP pipeline and SQLite database.
/// Every response is compared field-by-field with <see cref="SearchOracle"/>, an independent reference
/// implementation of UC-05 / BR-SRCH-01..06.
/// </summary>
[TestFixture]
public class SearchFilterCombinationsIntegrationTests
{
    private SearchTestFactory _factory = null!;
    private IReadOnlyList<SeedRoom> _dataset = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _dataset = SearchDatasetSeeder.CreateCombinationDataset();
        _factory = new SearchTestFactory();
        _factory.Seed(_dataset);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => _factory.Dispose();

    // ---------------------------------------------------------------------
    // Pairwise / partial / full filter combinations
    // ---------------------------------------------------------------------

    private static IEnumerable<TestCaseData> CombinationCases()
    {
        static TestCaseData Case(string name, RoomSearchRequest request) =>
            new TestCaseData(request).SetName($"FilterCombination_{name}");

        yield return Case("Default", new RoomSearchRequest());
        yield return Case("MinRentOnly", new RoomSearchRequest(MinRentVnd: 1_200_000));
        yield return Case("MaxRentOnly", new RoomSearchRequest(MaxRentVnd: 2_500_000));
        yield return Case("RentRange", new RoomSearchRequest(MinRentVnd: 1_200_000, MaxRentVnd: 2_500_000));
        yield return Case("RentExactBound", new RoomSearchRequest(MinRentVnd: 1_200_000, MaxRentVnd: 1_200_000));
        yield return Case("RentBelowBoundByOneVnd", new RoomSearchRequest(MinRentVnd: 1_199_999, MaxRentVnd: 1_199_999));
        yield return Case("RentAboveBoundByOneVnd", new RoomSearchRequest(MinRentVnd: 1_200_001, MaxRentVnd: 1_200_001));
        yield return Case("MinAreaOnly", new RoomSearchRequest(MinAreaM2: 15));
        yield return Case("MaxAreaOnly", new RoomSearchRequest(MaxAreaM2: 25));
        yield return Case("AreaExactBound", new RoomSearchRequest(MinAreaM2: 15, MaxAreaM2: 15));
        yield return Case("AreaRange", new RoomSearchRequest(MinAreaM2: 15, MaxAreaM2: 35));
        yield return Case("RoomTypeBoarding", new RoomSearchRequest(RoomTypes: new[] { RoomType.BoardingRoom }));
        yield return Case("RoomTypeMiniApartment", new RoomSearchRequest(RoomTypes: new[] { RoomType.MiniApartment }));
        yield return Case("RoomTypeWholeHouse", new RoomSearchRequest(RoomTypes: new[] { RoomType.WholeHouse }));
        yield return Case("RoomTypeDormitory", new RoomSearchRequest(RoomTypes: new[] { RoomType.Dormitory }));
        yield return Case("RoomTypesTwo", new RoomSearchRequest(RoomTypes: new[] { RoomType.BoardingRoom, RoomType.MiniApartment }));
        yield return Case("RoomTypesThree", new RoomSearchRequest(RoomTypes: new[] { RoomType.BoardingRoom, RoomType.MiniApartment, RoomType.WholeHouse }));
        yield return Case("RoomTypesAllFour", new RoomSearchRequest(RoomTypes: new[] { RoomType.BoardingRoom, RoomType.MiniApartment, RoomType.WholeHouse, RoomType.Dormitory }));
        yield return Case("RoomTypesDuplicated", new RoomSearchRequest(RoomTypes: new[] { RoomType.BoardingRoom, RoomType.BoardingRoom }));
        yield return Case("Distance0Point1", new RoomSearchRequest(MaxDistanceKm: 0.1));
        yield return Case("Distance1", new RoomSearchRequest(MaxDistanceKm: 1));
        yield return Case("Distance2", new RoomSearchRequest(MaxDistanceKm: 2));
        yield return Case("Distance3", new RoomSearchRequest(MaxDistanceKm: 3));
        yield return Case("Distance5", new RoomSearchRequest(MaxDistanceKm: 5));
        yield return Case("Distance10", new RoomSearchRequest(MaxDistanceKm: 10));
        yield return Case("Distance50", new RoomSearchRequest(MaxDistanceKm: 50));
        yield return Case("PriceAndArea", new RoomSearchRequest(MinRentVnd: 1_200_000, MaxRentVnd: 3_000_000, MinAreaM2: 15, MaxAreaM2: 35));
        yield return Case("PriceAndRoomTypes", new RoomSearchRequest(MinRentVnd: 1_200_000, MaxRentVnd: 3_000_000, RoomTypes: new[] { RoomType.BoardingRoom, RoomType.Dormitory }));
        yield return Case("PriceAndDistance", new RoomSearchRequest(MinRentVnd: 1_200_000, MaxRentVnd: 3_000_000, MaxDistanceKm: 2));
        yield return Case("AreaAndRoomTypes", new RoomSearchRequest(MinAreaM2: 15, MaxAreaM2: 35, RoomTypes: new[] { RoomType.MiniApartment, RoomType.WholeHouse }));
        yield return Case("AreaAndDistance", new RoomSearchRequest(MinAreaM2: 15, MaxAreaM2: 35, MaxDistanceKm: 3));
        yield return Case("RoomTypesAndDistance", new RoomSearchRequest(RoomTypes: new[] { RoomType.BoardingRoom }, MaxDistanceKm: 5));
        yield return Case("PriceAreaAndRoomTypes", new RoomSearchRequest(MinRentVnd: 1_200_000, MaxRentVnd: 3_000_000, MinAreaM2: 15, MaxAreaM2: 35, RoomTypes: new[] { RoomType.BoardingRoom, RoomType.MiniApartment }));
        yield return Case("AllFiltersWithCampusSortAndPaging", new RoomSearchRequest(
            CampusId: "TNUT",
            MinRentVnd: 1_200_000,
            MaxRentVnd: 10_000_000,
            MinAreaM2: 10,
            MaxAreaM2: 60,
            RoomTypes: new[] { RoomType.BoardingRoom, RoomType.Dormitory, RoomType.MiniApartment },
            MaxDistanceKm: 50,
            Sort: "price_asc",
            PageNumber: 1,
            PageSize: 7));
    }

    [TestCaseSource(nameof(CombinationCases))]
    [CancelAfter(30000)]
    public async Task FilterCombination_MatchesReferenceOracle(RoomSearchRequest request)
    {
        var page = await _factory.Client.GetSearchPageAsync(SearchApiClient.SearchUrl(request));
        var expectation = SearchOracle.Search(_dataset, request, request.CampusId ?? "ICTU");

        AssertMatchesOracle(page, expectation, request.PageNumber, request.PageSize);
    }

    [Test]
    public async Task RoomTypes_RepeatedQueryParameter_IsAccepted()
    {
        var page = await _factory.Client.GetSearchPageAsync(
            "/api/rooms?roomTypes=BoardingRoom&roomTypes=Dormitory&pageSize=50");
        var expectation = SearchOracle.Search(
            _dataset, new RoomSearchRequest(RoomTypes: new[] { RoomType.BoardingRoom, RoomType.Dormitory }, PageSize: 50));

        AssertMatchesOracle(page, expectation, 1, 50);
    }

    [Test]
    public async Task RoomTypes_CaseInsensitiveNames_AreAccepted()
    {
        var page = await _factory.Client.GetSearchPageAsync(
            "/api/rooms?roomTypes=boardingroom,MINIAPARTMENT&pageSize=50");
        var expectation = SearchOracle.Search(
            _dataset, new RoomSearchRequest(RoomTypes: new[] { RoomType.BoardingRoom, RoomType.MiniApartment }, PageSize: 50));

        AssertMatchesOracle(page, expectation, 1, 50);
    }

    // ---------------------------------------------------------------------
    // Inclusive boundaries
    // ---------------------------------------------------------------------

    [Test]
    public async Task RentBoundaries_OneVndOutsideRange_IsExcluded()
    {
        var exact = await _factory.Client.GetSearchPageAsync(
            "/api/rooms?minRentVnd=1200000&maxRentVnd=1200000&pageSize=50");
        var below = await _factory.Client.GetSearchPageAsync(
            "/api/rooms?minRentVnd=1199999&maxRentVnd=1199999&pageSize=50");
        var above = await _factory.Client.GetSearchPageAsync(
            "/api/rooms?minRentVnd=1200001&maxRentVnd=1200001&pageSize=50");

        Assert.Multiple(() =>
        {
            Assert.That(exact.Ids, Is.EqualTo(new[] { "c-0100-tenth" }));
            Assert.That(below.Ids, Is.EqualTo(new[] { "c-rent-1199999" }));
            Assert.That(above.Ids, Is.EqualTo(new[] { "c-rent-1200001" }));
        });
    }

    [Test]
    public async Task AreaBoundaries_OneTenthOutsideRange_IsExcluded()
    {
        var exact = await _factory.Client.GetSearchPageAsync(
            "/api/rooms?minAreaM2=15&maxAreaM2=15&pageSize=50");
        var below = await _factory.Client.GetSearchPageAsync(
            "/api/rooms?minAreaM2=14.9&maxAreaM2=14.9&pageSize=50");
        var above = await _factory.Client.GetSearchPageAsync(
            "/api/rooms?minAreaM2=15.1&maxAreaM2=15.1&pageSize=50");

        Assert.Multiple(() =>
        {
            Assert.That(exact.Ids, Is.EqualTo(new[] { "c-0100-tenth" }));
            Assert.That(below.Ids, Is.EqualTo(new[] { "c-area-149" }));
            Assert.That(above.Ids, Is.EqualTo(new[] { "c-area-151" }));
        });
    }

    [Test]
    public async Task TooTightCombination_ReturnsEmptyPageWithZeroTotals()
    {
        var page = await _factory.Client.GetSearchPageAsync(
            "/api/rooms?minRentVnd=49999999&maxRentVnd=50000000&minAreaM2=199&maxAreaM2=200&maxDistanceKm=0.1&roomTypes=Dormitory");

        Assert.Multiple(() =>
        {
            Assert.That(page.Items, Is.Empty);
            Assert.That(page.TotalCount, Is.Zero);
            Assert.That(page.TotalPages, Is.Zero);
            Assert.That(page.HasNextPage, Is.False);
            Assert.That(page.HasPreviousPage, Is.False);
        });
    }

    // ---------------------------------------------------------------------
    // Sorting and tie-breaks (BR-SRCH-04)
    // ---------------------------------------------------------------------

    private static IEnumerable<TestCaseData> SortCases()
    {
        foreach (var sort in new[] { "distance", "price_asc", "price_desc", "newest" })
        {
            yield return new TestCaseData(sort).SetName($"Sort_{sort}_WithFiltersMatchesOracle");
        }
    }

    [TestCaseSource(nameof(SortCases))]
    [CancelAfter(30000)]
    public async Task Sort_WithFilters_MatchesOracle(string sort)
    {
        var request = new RoomSearchRequest(
            MinRentVnd: 1_200_000,
            MaxRentVnd: 3_000_000,
            MinAreaM2: 15,
            MaxAreaM2: 35,
            MaxDistanceKm: 5,
            Sort: sort,
            PageSize: 50);

        var page = await _factory.Client.GetSearchPageAsync(SearchApiClient.SearchUrl(request));
        var expectation = SearchOracle.Search(_dataset, request);

        AssertMatchesOracle(page, expectation, 1, 50);
    }

    [Test]
    public async Task PriceAsc_EqualRents_AreTieBrokenByIdOrdinal()
    {
        var page = await _factory.Client.GetSearchPageAsync("/api/rooms?sort=price_asc&pageSize=50");

        AssertTieGroupsUseOrdinalIds(page, rent: true);
    }

    [Test]
    public async Task Newest_EqualPublishedTimes_AreTieBrokenByIdOrdinal()
    {
        var page = await _factory.Client.GetSearchPageAsync("/api/rooms?sort=newest&pageSize=50");

        AssertTieGroupsUseOrdinalIds(page, rent: false);
    }

    [Test]
    public async Task Distance_EqualRoundedDistances_AreTieBrokenByIdOrdinal()
    {
        var page = await _factory.Client.GetSearchPageAsync("/api/rooms?pageSize=50");

        AssertTieGroupsUseOrdinalIds(page, key: "distance");
    }

    [Test]
    public async Task Newest_NullPublishedAt_SortsLast()
    {
        // 54 visible rooms > page size 50, so the last four positions live on page 2.
        var page = await _factory.Client.GetSearchPageAsync("/api/rooms?sort=newest&pageSize=50&pageNumber=2");
        var nullPublishedIds = page.Items
            .Where(item => item.GetProperty("publishedAtUtc").ValueKind == System.Text.Json.JsonValueKind.Null)
            .Select(item => item.GetProperty("id").GetString())
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(page.Items, Has.Count.EqualTo(4));
            Assert.That(page.Ids[^1], Is.EqualTo("c-4000-nullpub"),
                "The only fixture room without PublishedAtUtc must sort last.");
            Assert.That(nullPublishedIds, Is.EqualTo(new[] { "c-4000-nullpub" }));
        });
    }

    // ---------------------------------------------------------------------
    // Campus changes (BR-CMP-03)
    // ---------------------------------------------------------------------

    [Test]
    public async Task CampusChange_AltersDistancesAndFilteredSet()
    {
        var ictu = await _factory.Client.GetSearchPageAsync(
            "/api/rooms?campusId=ICTU&maxDistanceKm=2&roomTypes=BoardingRoom&pageSize=50");
        var tnut = await _factory.Client.GetSearchPageAsync(
            "/api/rooms?campusId=TNUT&maxDistanceKm=2&roomTypes=BoardingRoom&pageSize=50");
        var tnue = await _factory.Client.GetSearchPageAsync(
            "/api/rooms?campusId=TNUE&maxDistanceKm=2&roomTypes=BoardingRoom&pageSize=50");

        var ictuExpectation = SearchOracle.Search(
            _dataset, new RoomSearchRequest(CampusId: "ICTU", MaxDistanceKm: 2, RoomTypes: new[] { RoomType.BoardingRoom }, PageSize: 50), "ICTU");
        var tnutExpectation = SearchOracle.Search(
            _dataset, new RoomSearchRequest(CampusId: "TNUT", MaxDistanceKm: 2, RoomTypes: new[] { RoomType.BoardingRoom }, PageSize: 50), "TNUT");
        var tnueExpectation = SearchOracle.Search(
            _dataset, new RoomSearchRequest(CampusId: "TNUE", MaxDistanceKm: 2, RoomTypes: new[] { RoomType.BoardingRoom }, PageSize: 50), "TNUE");

        AssertMatchesOracle(ictu, ictuExpectation, 1, 50);
        AssertMatchesOracle(tnut, tnutExpectation, 1, 50);
        AssertMatchesOracle(tnue, tnueExpectation, 1, 50);

        Assert.Multiple(() =>
        {
            Assert.That(ictu.Ids, Does.Not.Contain("t-near-tnut"), "TNUT-relative rooms are far from ICTU.");
            Assert.That(tnut.Ids, Contains.Item("t-near-tnut"));
            Assert.That(tnue.Ids, Contains.Item("t-near-tnue"));
            Assert.That(tnut.Ids, Is.Not.EquivalentTo(ictu.Ids));
        });
    }

    // ---------------------------------------------------------------------
    // Paging consistency (BR-SRCH-05)
    // ---------------------------------------------------------------------

    private static IEnumerable<TestCaseData> PageSizes()
    {
        yield return new TestCaseData(7).SetName("Paging_PageSize7_ConcatenationMatchesOracle");
        yield return new TestCaseData(20).SetName("Paging_PageSize20_ConcatenationMatchesOracle");
        yield return new TestCaseData(50).SetName("Paging_PageSize50_ConcatenationMatchesOracle");
    }

    [TestCaseSource(nameof(PageSizes))]
    [CancelAfter(60000)]
    public async Task Paging_ConsecutivePages_HaveNoGapsOrDuplicates(int pageSize)
    {
        var baseRequest = new RoomSearchRequest(
            MinRentVnd: 1_200_000,
            MaxRentVnd: 3_000_000,
            MinAreaM2: 15,
            MaxAreaM2: 35,
            MaxDistanceKm: 5,
            Sort: "price_asc");
        var expectation = SearchOracle.Search(_dataset, baseRequest);
        var totalPages = expectation.TotalPages(pageSize);

        var collected = new List<string>();
        for (var pageNumber = 1; pageNumber <= totalPages; pageNumber++)
        {
            var request = baseRequest with { PageNumber = pageNumber, PageSize = pageSize };
            var page = await _factory.Client.GetSearchPageAsync(SearchApiClient.SearchUrl(request));

            Assert.Multiple(() =>
            {
                Assert.That(page.TotalCount, Is.EqualTo(expectation.TotalCount), $"totalCount must be stable on page {pageNumber}.");
                Assert.That(page.PageNumber, Is.EqualTo(pageNumber));
                Assert.That(page.PageSize, Is.EqualTo(pageSize));
                Assert.That(page.TotalPages, Is.EqualTo(totalPages));
                Assert.That(page.HasPreviousPage, Is.EqualTo(pageNumber > 1));
                Assert.That(page.HasNextPage, Is.EqualTo(pageNumber < totalPages));
            });

            collected.AddRange(page.Ids);
        }

        Assert.Multiple(() =>
        {
            Assert.That(collected, Is.EqualTo(expectation.Ordered.Select(match => match.Id).ToList()),
                "Pages concatenated in order must equal the full ordered result (no gaps, no duplicates).");
            Assert.That(collected, Has.Count.EqualTo(collected.Distinct(StringComparer.Ordinal).Count()));
        });
    }

    [Test]
    public async Task Paging_PageBeyondLast_ReturnsEmptyItemsButKeepsTotals()
    {
        var request = new RoomSearchRequest(MinRentVnd: 1_200_000, MaxRentVnd: 3_000_000, Sort: "price_asc");
        var expectation = SearchOracle.Search(_dataset, request);

        var page = await _factory.Client.GetSearchPageAsync(
            SearchApiClient.SearchUrl(request with { PageNumber = 99, PageSize = 20 }));

        Assert.Multiple(() =>
        {
            Assert.That(page.Items, Is.Empty);
            Assert.That(page.TotalCount, Is.EqualTo(expectation.TotalCount));
            Assert.That(page.TotalPages, Is.EqualTo(expectation.TotalPages(20)));
            Assert.That(page.HasPreviousPage, Is.True);
            Assert.That(page.HasNextPage, Is.False);
        });
    }

    // ---------------------------------------------------------------------
    // Clear filters (BR-SRCH-06 / US04 AC4)
    // ---------------------------------------------------------------------

    [Test]
    public async Task ClearFilters_KeepsCampusAndRestoresFullDefaultList()
    {
        var narrowed = await _factory.Client.GetSearchPageAsync(
            "/api/rooms?campusId=ICTU&minRentVnd=1000000&maxRentVnd=2000000&minAreaM2=15&maxAreaM2=20&maxDistanceKm=1&roomTypes=BoardingRoom&sort=price_asc&pageSize=50");
        Assert.That(narrowed.TotalCount, Is.LessThan(54), "The filtered query must narrow the default list.");

        var cleared = await _factory.Client.GetSearchPageAsync("/api/rooms?campusId=ICTU&pageSize=50");
        var expectation = SearchOracle.Search(_dataset, new RoomSearchRequest(CampusId: "ICTU", PageSize: 50), "ICTU");

        AssertMatchesOracle(cleared, expectation, 1, 50);
        Assert.Multiple(() =>
        {
            Assert.That(cleared.TotalCount, Is.EqualTo(54), "Clear filters restores every publicly visible room.");
            Assert.That(cleared.Ids, Is.EqualTo(expectation.Page(1, 50).Select(match => match.Id).ToList()));
        });
    }

    // ---------------------------------------------------------------------

    private static void AssertMatchesOracle(SearchApiPage page, SearchExpectation expectation, int pageNumber, int pageSize)
    {
        var expected = expectation.Page(pageNumber, pageSize);
        var totalPages = expectation.TotalPages(pageSize);

        Assert.Multiple(() =>
        {
            Assert.That(page.TotalCount, Is.EqualTo(expectation.TotalCount), "BR-SRCH-05: totalCount is the filtered total.");
            Assert.That(page.PageNumber, Is.EqualTo(pageNumber));
            Assert.That(page.PageSize, Is.EqualTo(pageSize));
            Assert.That(page.TotalPages, Is.EqualTo(totalPages));
            Assert.That(page.HasNextPage, Is.EqualTo(pageNumber < totalPages));
            Assert.That(page.HasPreviousPage, Is.EqualTo(pageNumber > 1));
            Assert.That(page.Ids, Is.EqualTo(expected.Select(match => match.Id).ToList()));
            Assert.That(page.Distances, Is.EqualTo(expected.Select(match => match.DistanceKm).ToList()),
                "BR-SRCH-03: distanceKm is the Haversine distance rounded to 0.1 km.");
        });
    }

    /// <summary>
    /// Asserts that runs of equal primary keys keep ascending ordinal id order, i.e. the ThenBy(Id) tie-break.
    /// </summary>
    private static void AssertTieGroupsUseOrdinalIds(SearchApiPage page, bool rent = false, string? key = null)
    {
        var rows = page.Items.Select(item => new
        {
            Id = item.GetProperty("id").GetString()!,
            Primary = key == "distance"
                ? (object)item.GetProperty("distanceKm").GetDouble()
                : rent
                    ? (object)item.GetProperty("monthlyRentVnd").GetInt64()
                    : (object)(item.GetProperty("publishedAtUtc").ValueKind == System.Text.Json.JsonValueKind.Null
                        ? DateTime.MinValue
                        : item.GetProperty("publishedAtUtc").GetDateTime())
        }).ToList();

        for (var index = 1; index < rows.Count; index++)
        {
            if (Equals(rows[index - 1].Primary, rows[index].Primary))
            {
                Assert.That(
                    string.CompareOrdinal(rows[index - 1].Id, rows[index].Id), Is.LessThan(0),
                    $"Ids '{rows[index - 1].Id}' and '{rows[index].Id}' share the same sort key and must be ordered by ordinal id.");
            }
        }

        Assert.That(page.Ids, Is.Not.Empty, "Tie-break assertions need a non-empty page.");
    }
}
