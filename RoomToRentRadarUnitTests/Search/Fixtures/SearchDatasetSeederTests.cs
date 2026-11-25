using RoomToRentRadar.Ports.Contracts.Requests.Rooms;

namespace RoomToRentRadarUnitTests.Search.Fixtures;

/// <summary>Self-check of the T04.3 fixtures: row counts, adversarial mix and determinism.</summary>
[TestFixture]
public class SearchDatasetSeederTests
{
    [Test]
    public void CombinationDataset_HasExpectedVisibleNoiseSplit()
    {
        var dataset = SearchDatasetSeeder.CreateCombinationDataset();

        var visible = dataset.Count(room => room is
        {
            Status: RoomToRentRadar.Domain.Enums.ListingStatus.Available,
            LandlordIsVerified: true,
            VerifiedAtUtc: not null
        });

        Assert.Multiple(() =>
        {
            Assert.That(dataset, Has.Count.EqualTo(59));
            Assert.That(visible, Is.EqualTo(52));
            Assert.That(dataset.Count(room => room.Id.StartsWith("n-", StringComparison.Ordinal)), Is.EqualTo(7));
        });
    }

    [Test]
    public void CombinationDataset_SeededIntoApi_MatchesOracleTotals()
    {
        var dataset = SearchDatasetSeeder.CreateCombinationDataset();
        using var factory = new SearchTestFactory();
        factory.Seed(dataset);

        Assert.Multiple(() =>
        {
            Assert.That(factory.QueryScalar("SELECT COUNT(1) FROM Rooms;"), Is.EqualTo(59));
            Assert.That(
                factory.QueryScalar("SELECT COUNT(1) FROM Users WHERE Id IN ('ld-verified', 'ld-unverified');"),
                Is.EqualTo(2));
        });
    }

    [Test]
    public void PerformanceDataset_IsDeterministicAcrossRuns()
    {
        var first = SearchDatasetSeeder.CreatePerformanceDataset();
        var second = SearchDatasetSeeder.CreatePerformanceDataset();

        Assert.That(first.Select(RowKey), Is.EqualTo(second.Select(RowKey)));
        Assert.Multiple(() =>
        {
            Assert.That(first, Has.Count.EqualTo(1200));
            Assert.That(first.Count(room => room.Id.StartsWith("perf-", StringComparison.Ordinal)), Is.EqualTo(1000));
            Assert.That(first.Count(room => room.Id.StartsWith("noise-", StringComparison.Ordinal)), Is.EqualTo(200));
        });
    }

    [Test]
    public void PerformanceDataset_SeededIntoApi_MatchesOracleTotals()
    {
        var dataset = SearchDatasetSeeder.CreatePerformanceDataset();
        using var factory = new SearchTestFactory();
        factory.Seed(dataset);

        var expectation = SearchOracle.Search(dataset, new RoomSearchRequest(PageSize: 20));

        Assert.Multiple(() =>
        {
            Assert.That(factory.QueryScalar("SELECT COUNT(1) FROM Rooms;"), Is.EqualTo(1200));
            Assert.That(expectation.TotalCount, Is.EqualTo(1000), "BR-LST-06: 200 noise rows must never be visible.");
        });
    }

    private static string RowKey(SeedRoom room) =>
        string.Join('|', room.Id, room.MonthlyRentVnd, room.AreaM2, room.Latitude, room.Longitude, room.RoomType, room.Status);
}
