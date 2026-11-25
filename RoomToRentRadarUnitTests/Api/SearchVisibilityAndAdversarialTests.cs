using System.Net;
using System.Text.Json;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadarUnitTests.Search.Fixtures;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// US04 / T04.3 group 2 — BR-LST-06 visibility, BR-SRCH-02/UC-05 E1 adversarial input, SQL-injection
/// attempts, server-controlled parameter rejection and the Haversine rounding boundary against the SQL
/// bounding-box prefilter (BR-SRCH-03).
/// </summary>
[TestFixture]
public class SearchVisibilityAndAdversarialTests
{
    private static readonly string[] NoiseIds =
    {
        "n-pending", "n-rejected", "n-rented", "n-unpublished", "n-suspended",
        "n-avail-unverified-landlord", "n-avail-null-verified"
    };

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
    // BR-LST-06 / US01 AC4: visibility
    // ---------------------------------------------------------------------

    [Test]
    public async Task DefaultSearch_HidesEveryAdversarialRoom()
    {
        var page = await _factory.Client.GetSearchPageAsync("/api/rooms?pageSize=50");

        Assert.Multiple(() =>
        {
            Assert.That(page.TotalCount, Is.EqualTo(52), "Only Available + verified rooms with verified landlords are public.");
            Assert.That(page.Ids, Has.None.Matches<string>(id => NoiseIds.Contains(id, StringComparer.Ordinal)));
            Assert.That(page.Ids, Does.Not.Contain("n-avail-null-verified"),
                "BR-LST-06: an Available row without VerifiedAtUtc must never be exposed by search.");
        });
    }

    [Test]
    public async Task WideFilter_CoveringNoiseRooms_StillHidesThem()
    {
        var page = await _factory.Client.GetSearchPageAsync(
            "/api/rooms?minRentVnd=100000&maxRentVnd=50000000&minAreaM2=5&maxAreaM2=200&maxDistanceKm=50&pageSize=50");

        Assert.Multiple(() =>
        {
            Assert.That(page.Ids, Has.None.Matches<string>(id => NoiseIds.Contains(id, StringComparer.Ordinal)));
            Assert.That(page.TotalCount, Is.EqualTo(51), "52 visible rooms minus the one at 50.5 km.");
        });
    }

    [Test]
    public async Task AvailableRoom_WithUnverifiedLandlord_IsExcluded()
    {
        var page = await _factory.Client.GetSearchPageAsync("/api/rooms?roomTypes=BoardingRoom&pageSize=50");

        Assert.That(page.Ids, Does.Not.Contain("n-avail-unverified-landlord"));
    }

    [Test]
    public async Task AvailableRoom_WithoutVerifiedAtUtc_IsExcluded()
    {
        var page = await _factory.Client.GetSearchPageAsync("/api/rooms?roomTypes=BoardingRoom&pageSize=50");

        Assert.That(page.Ids, Does.Not.Contain("n-avail-null-verified"),
            "BR-LST-06: VerifiedAtUtc is the verification marker; an Available row without it is inconsistent data and must stay hidden.");
    }

    [Test]
    public async Task LandlordVerificationRevoked_RoomsDisappearOnNextSearch()
    {
        try
        {
            _factory.Execute("UPDATE Users SET IsVerified = 0 WHERE Id = 'ld-verified';");

            var page = await _factory.Client.GetSearchPageAsync("/api/rooms?pageSize=50");

            Assert.That(page.TotalCount, Is.Zero,
                "Every fixture room belongs to ld-verified, so revoking verification must empty the public feed.");
        }
        finally
        {
            _factory.Execute("UPDATE Users SET IsVerified = 1 WHERE Id = 'ld-verified';");
        }
    }

    // ---------------------------------------------------------------------
    // BR-SRCH-02 / UC-05 E1 / ADR-012: invalid input contract
    // ---------------------------------------------------------------------

    private static IEnumerable<TestCaseData> InvalidInputCases()
    {
        static TestCaseData Case(string url, string errorCode, bool expectsErrorsMap = false) =>
            new TestCaseData(url, errorCode, expectsErrorsMap).SetName(
                $"InvalidInput_{errorCode}_{new string(url.Where(char.IsLetterOrDigit).ToArray())}");

        // Price range
        yield return Case("/api/rooms?minRentVnd=-1", "INVALID_PRICE_RANGE");
        yield return Case("/api/rooms?maxRentVnd=-100", "INVALID_PRICE_RANGE");
        yield return Case("/api/rooms?minRentVnd=3000000&maxRentVnd=2999999", "INVALID_PRICE_RANGE");

        // Area range
        yield return Case("/api/rooms?minAreaM2=-0.1", "INVALID_AREA_RANGE");
        yield return Case("/api/rooms?maxAreaM2=-5", "INVALID_AREA_RANGE");
        yield return Case("/api/rooms?minAreaM2=30&maxAreaM2=29.9", "INVALID_AREA_RANGE");
        yield return Case("/api/rooms?minAreaM2=NaN", "INVALID_AREA_RANGE");
        yield return Case("/api/rooms?maxAreaM2=Infinity", "INVALID_AREA_RANGE");
        yield return Case("/api/rooms?minAreaM2=-Infinity", "INVALID_AREA_RANGE");

        // Distance range
        yield return Case("/api/rooms?maxDistanceKm=0", "INVALID_DISTANCE_RANGE");
        yield return Case("/api/rooms?maxDistanceKm=0.09", "INVALID_DISTANCE_RANGE");
        yield return Case("/api/rooms?maxDistanceKm=-1", "INVALID_DISTANCE_RANGE");
        yield return Case("/api/rooms?maxDistanceKm=50.01", "INVALID_DISTANCE_RANGE");
        yield return Case("/api/rooms?maxDistanceKm=100", "INVALID_DISTANCE_RANGE");
        yield return Case("/api/rooms?maxDistanceKm=NaN", "INVALID_DISTANCE_RANGE");
        yield return Case("/api/rooms?maxDistanceKm=Infinity", "INVALID_DISTANCE_RANGE");
        yield return Case("/api/rooms?maxDistanceKm=-Infinity", "INVALID_DISTANCE_RANGE");

        // Sort whitelist (ruling 21: absent = default, present-but-empty = invalid)
        yield return Case("/api/rooms?sort=", "INVALID_SORT_OPTION");
        yield return Case("/api/rooms?sort=%20%20", "INVALID_SORT_OPTION");
        yield return Case("/api/rooms?sort=DISTANCE", "INVALID_SORT_OPTION");
        yield return Case("/api/rooms?sort=Price_Asc", "INVALID_SORT_OPTION");
        yield return Case("/api/rooms?sort=area_asc", "INVALID_SORT_OPTION");
        yield return Case("/api/rooms?sort=distance%3BDROP%20TABLE%20Rooms", "INVALID_SORT_OPTION");

        // Room type whitelist (names only, no numbers, no unknown values)
        yield return Case("/api/rooms?roomTypes=1", "VALIDATION_FAILED", expectsErrorsMap: true);
        yield return Case("/api/rooms?roomTypes=BoardingRoom,Bogus", "VALIDATION_FAILED", expectsErrorsMap: true);
        yield return Case("/api/rooms?roomTypes=BoardingRoom,2", "VALIDATION_FAILED", expectsErrorsMap: true);

        // Campus whitelist
        yield return Case("/api/rooms?campusId=UNKNOWN", "CAMPUS_NOT_FOUND");
        yield return Case("/api/rooms?campusId=%27%20OR%201%3D1%20--", "CAMPUS_NOT_FOUND");

        // SQL-injection payload in a whitelisted enum parameter
        yield return Case("/api/rooms?roomTypes=BoardingRoom%27%20OR%20%271%27%3D%271", "VALIDATION_FAILED", expectsErrorsMap: true);
    }

    [TestCaseSource(nameof(InvalidInputCases))]
    [CancelAfter(30000)]
    public async Task InvalidInput_Returns400ProblemDetailsWithExactErrorCode(string url, string expectedErrorCode, bool expectsErrorsMap)
    {
        var problem = await _factory.Client.GetProblemAsync(url);

        Assert.Multiple(() =>
        {
            Assert.That(problem.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), url);
            Assert.That(problem.ContentType, Is.EqualTo("application/problem+json"), url);
            Assert.That(problem.ErrorCode, Is.EqualTo(expectedErrorCode), url);

            if (expectsErrorsMap)
            {
                using var document = JsonDocument.Parse(problem.Body);
                Assert.That(document.RootElement.TryGetProperty("errors", out var errors), Is.True,
                    "ADR-012: whitelist failures expose a field-level errors map.");
                Assert.That(errors.ValueKind, Is.EqualTo(JsonValueKind.Object));
            }
        });
    }

    [Test]
    public async Task ServerControlledQueryParameters_AreIgnored()
    {
        var page = await _factory.Client.GetSearchPageAsync(
            "/api/rooms?status=PendingReview&isVerified=false&landlordId=ld-unverified&pageSize=50");
        var expectation = SearchOracle.Search(_dataset, new RoomSearchRequest(PageSize: 50));

        Assert.Multiple(() =>
        {
            Assert.That(page.TotalCount, Is.EqualTo(expectation.TotalCount));
            Assert.That(page.Ids, Is.EqualTo(expectation.Page(1, 50).Select(match => match.Id).ToList()));
            Assert.That(page.Items.Select(item => item.GetProperty("status").GetString()), Is.All.EqualTo("Available"));
            Assert.That(page.Items.Select(item => item.GetProperty("isVerified").GetBoolean()), Is.All.True);
        });
    }

    [Test]
    public async Task SqlInjectionAttempts_LeaveDatabaseIntact()
    {
        await _factory.Client.GetProblemAsync("/api/rooms?campusId=%27%3B%20DROP%20TABLE%20Rooms%3B%20--");
        await _factory.Client.GetProblemAsync("/api/rooms?roomTypes=%27%3B%20DELETE%20FROM%20Rooms%3B%20--");

        var page = await _factory.Client.GetSearchPageAsync("/api/rooms?pageSize=50");

        Assert.Multiple(() =>
        {
            Assert.That(_factory.QueryScalar("SELECT COUNT(1) FROM Rooms;"), Is.EqualTo(59));
            Assert.That(page.TotalCount, Is.EqualTo(52));
        });
    }

    // ---------------------------------------------------------------------
    // BR-SRCH-03 / ADR-006: Haversine rounding boundary vs bounding-box prefilter
    // ---------------------------------------------------------------------

    [Test]
    public async Task DistanceBoundary_RoomRoundedToOneKm_IsIncludedAtOneKm()
    {
        var page = await _factory.Client.GetSearchPageAsync("/api/rooms?maxDistanceKm=1&roomTypes=MiniApartment&pageSize=50");
        var expectation = SearchOracle.Search(
            _dataset, new RoomSearchRequest(MaxDistanceKm: 1, RoomTypes: new[] { RoomType.MiniApartment }, PageSize: 50));

        AssertMatchesVisibilityOracle(page, expectation);
        Assert.Multiple(() =>
        {
            Assert.That(page.Ids, Does.Contain("c-0950"), "0.95 km rounds to 1.0 km.");
            Assert.That(page.Ids, Does.Contain("c-1040"),
                "1.04 km rounds to 1.0 km (BR-SRCH-03) and must not be lost to a too-narrow SQL bounding box.");
        });
    }

    [Test]
    public async Task DistanceBoundary_RoomRoundedToElevenKm_IsExcludedAtOneKm()
    {
        var page = await _factory.Client.GetSearchPageAsync("/api/rooms?maxDistanceKm=1&roomTypes=WholeHouse&pageSize=50");

        Assert.That(page.Ids, Does.Not.Contain("c-1060"), "1.06 km rounds to 1.1 km, above the 1 km filter.");
    }

    [Test]
    public async Task DistanceBoundary_DiagonalInsideBoundingBoxButBeyondRadius_IsExcluded()
    {
        var page = await _factory.Client.GetSearchPageAsync("/api/rooms?maxDistanceKm=1&pageSize=50");
        var expectation = SearchOracle.Search(_dataset, new RoomSearchRequest(MaxDistanceKm: 1, PageSize: 50));

        AssertMatchesVisibilityOracle(page, expectation);
        Assert.That(page.Ids, Does.Not.Contain("c-1485-diag"),
            "Inside the square bounding box (~1.4 km diagonal) but outside the 1 km circle; the exact Haversine cut must drop it.");
    }

    [Test]
    public async Task DistanceBoundary_InclusiveAtTenthOfKm()
    {
        var page = await _factory.Client.GetSearchPageAsync("/api/rooms?maxDistanceKm=0.1&pageSize=50");

        Assert.Multiple(() =>
        {
            Assert.That(page.Ids, Contains.Item("c-0000-zero"));
            Assert.That(page.Ids, Contains.Item("c-0100-tenth"), "0.1 km is inclusive (<= maxDistanceKm).");
            Assert.That(page.Ids, Does.Not.Contain("c-fill-00"), "0.4 km filler room is outside 0.1 km.");
        });
    }

    private static void AssertMatchesVisibilityOracle(SearchApiPage page, SearchExpectation expectation)
    {
        Assert.Multiple(() =>
        {
            Assert.That(page.TotalCount, Is.EqualTo(expectation.TotalCount));
            Assert.That(page.Ids, Is.EqualTo(expectation.Page(1, 50).Select(match => match.Id).ToList()));
            Assert.That(page.Distances, Is.EqualTo(expectation.Page(1, 50).Select(match => match.DistanceKm).ToList()));
        });
    }
}
