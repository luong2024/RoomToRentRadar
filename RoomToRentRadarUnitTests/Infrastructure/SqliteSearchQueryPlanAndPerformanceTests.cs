using System.Diagnostics;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadarUnitTests.Search.Fixtures;

namespace RoomToRentRadarUnitTests.Infrastructure;

/// <summary>
/// US04 / T04.3 group 5a — proves the search read model can use the ERD indexes
/// (<c>IX_Rooms_Status_Rent</c>, <c>IX_Rooms_Status_Lat_Lng</c>) instead of a full table scan, and measures
/// the read model latency on the 1,000-room dataset (NFR-02 evidence).
/// </summary>
/// <remarks>
/// The SQL below mirrors the WHERE clause produced by <c>SqliteRoomSearchReadModel</c> (T04.1 + T04.3
/// dynamic filter). Keep the two in sync: the point of this test is to catch a regression where a filter is
/// added to the read model in a shape SQLite cannot push into an index.
/// </remarks>
[TestFixture]
public class SqliteSearchQueryPlanAndPerformanceTests
{
    private const string SelectPrefix = """
        SELECT r.Id, r.Title, r.MonthlyRentVnd, r.AreaM2, r.RoomType, r.Status,
               (r.VerifiedAtUtc IS NOT NULL) AS IsVerified,
               NULL AS CoverThumbUrl,
               r.Latitude, r.Longitude, r.PublishedAtUtc
        FROM Rooms r
        INNER JOIN Users u ON u.Id = r.LandlordId
        WHERE r.Status = 'Available'
          AND u.IsVerified = 1
          AND r.VerifiedAtUtc IS NOT NULL
        """;

    private SqliteDbContext _dbContext = null!;
    private SqliteConnection _connection = null!;
    private IReadOnlyList<SeedRoom> _dataset = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _dataset = SearchDatasetSeeder.CreatePerformanceDataset();
        _dbContext = new SqliteDbContext(new SqliteSettings
        {
            ConnectionString = "Data Source=:memory:",
            SeedDemoRooms = false
        });
        _connection = _dbContext.CreateOpenConnection();
        SearchDatasetSeeder.Seed(_connection, _dataset);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        // The :memory: connection is also the SqliteDbContext keep-alive connection; disposing it here and
        // again through the context is safe (SqliteConnection.Dispose is idempotent).
        _connection.Dispose();
        _dbContext.Dispose();
    }

    [Test]
    public void QueryPlan_RentRange_UsesStatusRentIndexWithoutFullScan()
    {
        var plan = Explain(
            SelectPrefix + """

              AND r.MonthlyRentVnd >= @MinRent
              AND r.MonthlyRentVnd <= @MaxRent
            """,
            ("@MinRent", 1_000_000L),
            ("@MaxRent", 3_000_000L));

        PrintPlan("rent range", plan);

        Assert.Multiple(() =>
        {
            Assert.That(plan, Has.Some.Matches<string>(line => line.Contains("SEARCH r USING INDEX IX_Rooms_Status_Rent", StringComparison.Ordinal)),
                "A rent range must be answered by IX_Rooms_Status_Rent.");
            Assert.That(plan, Has.None.Matches<string>(line => line.Contains("SCAN r", StringComparison.Ordinal)), "No full table scan on Rooms.");
        });
    }

    [Test]
    public void QueryPlan_BoundingBox_UsesStatusLatLngIndexWithoutFullScan()
    {
        var plan = Explain(
            SelectPrefix + """

              AND r.Latitude >= @MinLat
              AND r.Latitude <= @MaxLat
              AND r.Longitude >= @MinLng
              AND r.Longitude <= @MaxLng
            """,
            ("@MinLat", 21.56),
            ("@MaxLat", 21.61),
            ("@MinLng", 105.78),
            ("@MaxLng", 105.84));

        PrintPlan("bounding box", plan);

        Assert.Multiple(() =>
        {
            Assert.That(plan, Has.Some.Matches<string>(line => line.Contains("SEARCH r USING INDEX IX_Rooms_Status_Lat_Lng", StringComparison.Ordinal)),
                "The bounding-box prefilter must use IX_Rooms_Status_Lat_Lng (ADR-006).");
            Assert.That(plan, Has.None.Matches<string>(line => line.Contains("SCAN r", StringComparison.Ordinal)), "No full table scan on Rooms.");
        });
    }

    [Test]
    public void QueryPlan_CombinedFilters_UsesAnIndexAndNeverScansRooms()
    {
        var plan = Explain(
            SelectPrefix + """

              AND r.MonthlyRentVnd >= @MinRent
              AND r.MonthlyRentVnd <= @MaxRent
              AND r.AreaM2 >= @MinArea
              AND r.AreaM2 <= @MaxArea
              AND r.Latitude >= @MinLat
              AND r.Latitude <= @MaxLat
              AND r.RoomType IN (@RoomType)
            """,
            ("@MinRent", 1_000_000L),
            ("@MaxRent", 3_000_000L),
            ("@MinArea", 15d),
            ("@MaxArea", 35d),
            ("@MinLat", 21.56),
            ("@MaxLat", 21.61),
            ("@RoomType", "BoardingRoom"));

        PrintPlan("combined filters", plan);

        Assert.Multiple(() =>
        {
            Assert.That(plan, Has.Some.Matches<string>(line => line.Contains("SEARCH r USING", StringComparison.Ordinal)),
                "At least one Rooms index must be used for the combined filter query.");
            Assert.That(plan, Has.None.Matches<string>(line => line.Contains("SCAN r", StringComparison.Ordinal)), "No full table scan on Rooms.");
        });
    }

    [Test]
    [CancelAfter(60000)]
    public async Task ReadModel_OnOneThousandRooms_MeetsNfr02Latency()
    {
        IRoomSearchReadModel readModel = new SqliteRoomSearchReadModel(_dbContext);
        var filters = RepresentativeFilters();

        for (var warmup = 0; warmup < 5; warmup++)
        {
            await readModel.QueryCandidatesAsync(filters[warmup % filters.Count]);
        }

        var samples = new List<double>(40);
        for (var iteration = 0; iteration < 40; iteration++)
        {
            var stopwatch = Stopwatch.StartNew();
            var candidates = await readModel.QueryCandidatesAsync(filters[iteration % filters.Count]);
            stopwatch.Stop();

            Assert.That(candidates, Is.Not.Null);
            Assert.That(candidates.Select(candidate => candidate.Status), Is.All.EqualTo(ListingStatus.Available));
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        var min = samples.Min();
        var p50 = Percentile(samples, 50);
        var p95 = Percentile(samples, 95);
        var max = samples.Max();
        TestContext.Progress.WriteLine(
            $"[T04.3] ReadModel on 1,000 rooms: min={min:F2}ms p50={p50:F2}ms p95={p95:F2}ms max={max:F2}ms");

        Assert.Multiple(() =>
        {
            Assert.That(p95, Is.LessThanOrEqualTo(300), $"NFR-02: read-model p95 {p95:F2} ms must be <= 300 ms.");
            Assert.That(max, Is.LessThan(2000), $"US04 AC2: worst-case read {max:F2} ms must be < 2,000 ms.");
        });
    }

    private static IReadOnlyList<RoomSearchQueryFilter> RepresentativeFilters() => new[]
    {
        new RoomSearchQueryFilter(null, null, null, null, null, null, null, null, null),
        new RoomSearchQueryFilter(1_000_000, 3_000_000, null, null, null, null, null, null, null),
        new RoomSearchQueryFilter(null, null, 15, 35, null, null, null, null, null),
        new RoomSearchQueryFilter(null, null, null, null, new[] { RoomType.BoardingRoom }, null, null, null, null),
        new RoomSearchQueryFilter(null, null, null, null, null, 21.56, 21.61, 105.78, 105.84),
        new RoomSearchQueryFilter(1_000_000, 3_000_000, 15, 35, new[] { RoomType.BoardingRoom, RoomType.Dormitory }, 21.56, 21.61, 105.78, 105.84),
        new RoomSearchQueryFilter(null, 1_500_000, null, 20, new[] { RoomType.MiniApartment }, null, null, null, null),
        new RoomSearchQueryFilter(2_000_000, null, 20, null, null, null, null, null, null)
    };

    private List<string> Explain(string sql, params (string Name, object Value)[] parameters)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var plan = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            plan.Add(reader.GetString(3));
        }

        return plan;
    }

    private static void PrintPlan(string label, IReadOnlyList<string> plan)
    {
        TestContext.Progress.WriteLine($"[T04.3] EXPLAIN QUERY PLAN ({label}):");
        foreach (var line in plan)
        {
            TestContext.Progress.WriteLine("  " + line);
        }
    }

    private static double Percentile(List<double> samples, double percentile)
    {
        var sorted = samples.OrderBy(value => value).ToList();
        var rank = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
    }
}
