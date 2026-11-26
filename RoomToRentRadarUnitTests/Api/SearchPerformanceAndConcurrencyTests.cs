using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Client.Adapters;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Mocks;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Navigation.Core;
using RoomToRentRadar.Services.Search.Core;
using RoomToRentRadarUnitTests.Search.Fixtures;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// US04 / T04.3 group 5b — NFR-02 (API p95 <= 300 ms at 1,000 listings), US04 AC2 (client cycle
/// &lt; 2,000 ms), NFR-01 (first page of &gt;= 500 listings &lt; 3,000 ms) and 20 concurrent HTTP readers
/// against a real SQLite file.
/// </summary>
[TestFixture]
public class SearchPerformanceAndConcurrencyTests
{
    private static readonly CultureInfo En = new("en-US");

    private SearchTestFactory _memoryFactory = null!;
    private ServiceProvider _provider = null!;
    private IRoomGateway _gateway = null!;
    private IReadOnlyList<SeedRoom> _dataset = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        LocalizedStrings.Instance.SetCulture(En);
        _dataset = SearchDatasetSeeder.CreatePerformanceDataset();
        _memoryFactory = new SearchTestFactory();
        _memoryFactory.Seed(_dataset);
        _memoryFactory.SeedPhotos(_dataset);

        var services = new ServiceCollection();
        services.AddSingleton<ISessionStore>(new InMemorySessionStore());
        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler(() => _memoryFactory.WebApp.Server.CreateHandler()));
        services.AddClientAdapters(new ApiOptions { BaseUrl = new Uri("http://localhost") });
        _provider = services.BuildServiceProvider();
        _gateway = _provider.GetRequiredService<IRoomGateway>();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _provider.Dispose();
        _memoryFactory.Dispose();
        LocalizedStrings.Instance.SetCulture(En);
    }

    // ---------------------------------------------------------------------
    // NFR-02 / US04 AC2: API latency on 1,000 Available listings
    // ---------------------------------------------------------------------

    private static IReadOnlyList<RoomSearchRequest> RepresentativeRequests() => new[]
    {
        new RoomSearchRequest(),
        new RoomSearchRequest(MinRentVnd: 1_000_000, MaxRentVnd: 3_000_000),
        new RoomSearchRequest(MinAreaM2: 15, MaxAreaM2: 35),
        new RoomSearchRequest(RoomTypes: new[] { RoomType.BoardingRoom }),
        new RoomSearchRequest(RoomTypes: new[] { RoomType.Dormitory, RoomType.MiniApartment }),
        new RoomSearchRequest(MaxDistanceKm: 1),
        new RoomSearchRequest(MaxDistanceKm: 2),
        new RoomSearchRequest(MaxDistanceKm: 3),
        new RoomSearchRequest(MaxDistanceKm: 5),
        new RoomSearchRequest(MaxDistanceKm: 10),
        new RoomSearchRequest(MinRentVnd: 1_000_000, MaxRentVnd: 3_000_000, MinAreaM2: 15, MaxAreaM2: 35, RoomTypes: new[] { RoomType.BoardingRoom }, MaxDistanceKm: 3),
        new RoomSearchRequest(Sort: "price_asc"),
        new RoomSearchRequest(Sort: "price_desc"),
        new RoomSearchRequest(Sort: "newest"),
        new RoomSearchRequest(CampusId: "TNUT"),
        new RoomSearchRequest(CampusId: "TUAF"),
        new RoomSearchRequest(PageSize: 1),
        new RoomSearchRequest(PageSize: 50),
        new RoomSearchRequest(MinRentVnd: 2_000_000, MaxAreaM2: 25, MaxDistanceKm: 5, Sort: "price_asc"),
        new RoomSearchRequest(CampusId: "TNUE", MaxDistanceKm: 2, RoomTypes: new[] { RoomType.WholeHouse })
    };

    [Test]
    [CancelAfter(120000)]
    public async Task ApiSearch_OnOneThousandRooms_MeetsNfr02P95AndAc2Max()
    {
        var requests = RepresentativeRequests();

        for (var warmup = 0; warmup < 5; warmup++)
        {
            await _memoryFactory.Client.GetSearchPageAsync(SearchApiClient.SearchUrl(requests[warmup % requests.Count]));
        }

        var samples = new List<double>(40);
        for (var iteration = 0; iteration < 40; iteration++)
        {
            var request = requests[iteration % requests.Count];
            var stopwatch = Stopwatch.StartNew();
            var page = await _memoryFactory.Client.GetSearchPageAsync(SearchApiClient.SearchUrl(request));
            stopwatch.Stop();

            AssertPageHasNoNoise(page, request);
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        var min = samples.Min();
        var p50 = Percentile(samples, 50);
        var p95 = Percentile(samples, 95);
        var max = samples.Max();
        TestContext.Progress.WriteLine(
            $"[T04.3] API search on 1,000 rooms (40 requests): min={min:F2}ms p50={p50:F2}ms p95={p95:F2}ms max={max:F2}ms");

        Assert.Multiple(() =>
        {
            Assert.That(p95, Is.LessThanOrEqualTo(300), $"NFR-02: API p95 {p95:F2} ms must be <= 300 ms.");
            Assert.That(max, Is.LessThan(2000), $"US04 AC2: worst case {max:F2} ms must be < 2,000 ms.");
        });
    }

    // ---------------------------------------------------------------------
    // NFR-01 / US01 AC3: first page of >= 500 listings
    // ---------------------------------------------------------------------

    [Test]
    [CancelAfter(60000)]
    public async Task FirstPage_OfOneThousandRooms_LoadsWithinBudget()
    {
        var stopwatch = Stopwatch.StartNew();
        var page = await _memoryFactory.Client.GetSearchPageAsync("/api/rooms?pageNumber=1&pageSize=20");
        stopwatch.Stop();

        TestContext.Progress.WriteLine($"[T04.3] NFR-01 first page (20 of 1,000): {stopwatch.Elapsed.TotalMilliseconds:F2} ms");

        Assert.Multiple(() =>
        {
            Assert.That(page.TotalCount, Is.EqualTo(1000), "The 200 noise rows must stay hidden (BR-LST-06).");
            Assert.That(page.Items, Has.Count.EqualTo(20));
            Assert.That(stopwatch.Elapsed.TotalMilliseconds, Is.LessThan(3000), "NFR-01: first page <= 3,000 ms.");
        });
    }

    // ---------------------------------------------------------------------
    // US04 AC2: full client cycle (Filters live count + Apply + Explore first page)
    // ---------------------------------------------------------------------

    [Test]
    [CancelAfter(60000)]
    public async Task ClientUpdateCycle_LiveCountThenExploreFirstPage_FinishesUnderTwoSeconds()
    {
        var store = new SearchCriteriaStore();
        var navigation = Substitute.For<INavigationService>();
        var filters = new FiltersPageModel(_gateway, store, navigation, TimeSpan.FromMilliseconds(50));
        await filters.InitializeAsync();

        var landing = new MainLandingPageModel(_gateway, navigation, store);
        await landing.InitializeAsync();

        var criteria = new RoomSearchRequest(
            MaxRentVnd: 3_000_000,
            MaxDistanceKm: 3,
            RoomTypes: new[] { RoomType.BoardingRoom });
        var expectation = SearchOracle.Search(_dataset, criteria);

        var stopwatch = Stopwatch.StartNew();
        filters.MaxRentText = "3000000";
        filters.SelectMaxDistanceCommand.Execute("3");
        filters.ToggleRoomTypeCommand.Execute(RoomType.BoardingRoom);

        await WaitUntilAsync(
            () => filters.HasLiveResultCount && !filters.IsCounting && filters.LiveResultCount == expectation.TotalCount,
            "live count matches the oracle");

        await filters.ApplyCommand.ExecuteAsync(null);
        await WaitUntilAsync(
            () => landing.TotalRoomCount == expectation.TotalCount &&
                  landing.Rooms.Select(room => room.Id)
                      .SequenceEqual(expectation.Page(1, 20).Select(match => match.Id)),
            "Explore shows the filtered first page");
        stopwatch.Stop();

        TestContext.Progress.WriteLine($"[T04.3] Client update cycle: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");

        Assert.Multiple(() =>
        {
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(2000), "US04 AC2: filter change to updated results <= 2,000 ms.");
            Assert.That(filters.LiveResultCount, Is.EqualTo(landing.TotalRoomCount));
            Assert.That(expectation.TotalCount, Is.GreaterThan(0));
        });
    }

    // ---------------------------------------------------------------------
    // Concurrent readers on a real SQLite file
    // ---------------------------------------------------------------------

    [Test]
    [CancelAfter(120000)]
    public async Task ConcurrentReaders_TwentyParallelRequests_AllSucceedAndMatchOracle()
    {
        using var fileFactory = new SearchTestFactory(useFileDatabase: true);
        fileFactory.Seed(_dataset);
        fileFactory.SeedPhotos(_dataset);

        var requests = new List<RoomSearchRequest>();
        foreach (var campusId in new string?[] { null, "ICTU", "TNUT", "TNUE", "TUAF" })
        {
            foreach (var distance in new double[] { 1, 2, 3, 5 })
            {
                requests.Add(new RoomSearchRequest(CampusId: campusId, MaxDistanceKm: distance, PageSize: 20));
            }
        }

        Assert.That(requests, Has.Count.EqualTo(20));

        var tasks = requests.Select(request => MeasureAsync(fileFactory.Client, request)).ToList();
        var results = await Task.WhenAll(tasks);

        var samples = new List<double>(results.Length);
        foreach (var (request, result) in requests.Zip(results))
        {
            samples.Add(result.ElapsedMs);

            var expectation = SearchOracle.Search(_dataset, request, request.CampusId ?? "ICTU");
            Assert.Multiple(() =>
            {
                Assert.That(result.Page.TotalCount, Is.EqualTo(expectation.TotalCount),
                    $"Concurrent request changed the result set: {SearchApiClient.SearchUrl(request)}");
                Assert.That(result.Page.Ids, Is.EqualTo(expectation.Page(1, 20).Select(match => match.Id).ToList()),
                    $"Concurrent request returned different rooms: {SearchApiClient.SearchUrl(request)}");
            });
        }

        var p50 = Percentile(samples, 50);
        var p95 = Percentile(samples, 95);
        var max = samples.Max();
        TestContext.Progress.WriteLine(
            $"[T04.3] 20 concurrent readers (file SQLite): p50={p50:F2}ms p95={p95:F2}ms max={max:F2}ms");

        // GetSearchPageAsync throws on any non-2xx response, so reaching here already proves 20/20 returned 200.
        Assert.Multiple(() =>
        {
            Assert.That(samples, Has.Count.EqualTo(20));
            Assert.That(p95, Is.LessThanOrEqualTo(300), $"Concurrent p95 {p95:F2} ms must stay within the NFR-02 budget.");
            Assert.That(max, Is.LessThan(2000), $"US04 AC2: concurrent worst case {max:F2} ms must be < 2,000 ms.");
        });
    }

    [Test]
    public void FileDatabase_IsRemovedWhenTheFactoryIsDisposed()
    {
        var factory = new SearchTestFactory(useFileDatabase: true);
        var path = factory.DatabasePath!;
        factory.Seed(SearchDatasetSeeder.CreateCombinationDataset());

        Assert.That(File.Exists(path), Is.True);
        factory.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(path), Is.False, "T04.3 DoD: temp .db files must be cleaned up.");
            Assert.That(File.Exists(path + "-wal"), Is.False);
            Assert.That(File.Exists(path + "-shm"), Is.False);
        });
    }

    // ---------------------------------------------------------------------

    private async Task<(SearchApiPage Page, double ElapsedMs)> MeasureAsync(HttpClient client, RoomSearchRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var page = await client.GetSearchPageAsync(SearchApiClient.SearchUrl(request));
        stopwatch.Stop();
        return (page, stopwatch.Elapsed.TotalMilliseconds);
    }

    private static void AssertPageHasNoNoise(SearchApiPage page, RoomSearchRequest request)
    {
        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Select(item => item.GetProperty("status").GetString()), Is.All.EqualTo("Available"),
                SearchApiClient.SearchUrl(request));
            Assert.That(page.Items.Select(item => item.GetProperty("isVerified").GetBoolean()), Is.All.True,
                SearchApiClient.SearchUrl(request));
            Assert.That(
                page.Items.All(item =>
                {
                    var id = item.GetProperty("id").GetString()!;
                    var coverThumbUrl = item.GetProperty("coverThumbUrl").GetString();
                    return coverThumbUrl is not null &&
                           coverThumbUrl.EndsWith(
                               "/" + SearchDatasetSeeder.ExpectedCoverThumbPath(id),
                               StringComparison.Ordinal);
                }),
                Is.True,
                $"BR-MED-05: coverThumbUrl must resolve the Standard cover photo for {SearchApiClient.SearchUrl(request)}.");
        });
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string because, int timeoutMilliseconds = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail($"Timed out after {timeoutMilliseconds} ms waiting until {because}.");
            }

            await Task.Delay(10);
        }
    }

    private static double Percentile(List<double> samples, double percentile)
    {
        var sorted = samples.OrderBy(value => value).ToList();
        var rank = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
    }
}
