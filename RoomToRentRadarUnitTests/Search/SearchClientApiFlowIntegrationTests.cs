using System.Diagnostics;
using System.Globalization;
using RoomToRentRadar.Client.Adapters;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Mocks;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.PageModels.Base;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Navigation.Core;
using RoomToRentRadar.Services.Search.Core;
using RoomToRentRadarUnitTests.Search.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace RoomToRentRadarUnitTests.Search;

/// <summary>
/// US04 / T04.3 group 4 — the real client stack (<see cref="FiltersPageModel"/>,
/// <see cref="SearchCriteriaStore"/>, <see cref="CampusSearchBridge"/>, <see cref="MainLandingPageModel"/>,
/// <see cref="HttpRoomGateway"/>) wired to the real API through <c>WebApplicationFactory</c> and SQLite.
/// No mock gateway: live count, chips, paging, clear filters, debounce and vi-VN formatting are all verified
/// against the reference oracle.
/// </summary>
[TestFixture]
public class SearchClientApiFlowIntegrationTests
{
    private static readonly CultureInfo En = new("en-US");
    private static readonly CultureInfo Vi = new("vi-VN");

    private SearchTestFactory _factory = null!;
    private ServiceProvider _provider = null!;
    private IRoomGateway _gateway = null!;
    private IReadOnlyList<SeedRoom> _dataset = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _dataset = SearchDatasetSeeder.CreateCombinationDataset();
        _factory = new SearchTestFactory();
        _factory.Seed(_dataset);

        var services = new ServiceCollection();
        services.AddSingleton<ISessionStore>(new InMemorySessionStore());
        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler(() => _factory.WebApp.Server.CreateHandler()));
        services.AddClientAdapters(new ApiOptions { BaseUrl = new Uri("http://localhost") });
        _provider = services.BuildServiceProvider();
        _gateway = _provider.GetRequiredService<IRoomGateway>();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _provider.Dispose();
        _factory.Dispose();
    }

    [SetUp]
    public void SetUp() => LocalizedStrings.Instance.SetCulture(En);

    [TearDown]
    public void TearDown() => LocalizedStrings.Instance.SetCulture(En);

    // ---------------------------------------------------------------------
    // Live count -> Apply -> Explore count, chips and sort (US04 AC2/AC3)
    // ---------------------------------------------------------------------

    [Test]
    [CancelAfter(30000)]
    public async Task LiveCount_ThenApply_KeepsExploreCountChipsAndSortInSync()
    {
        var store = new SearchCriteriaStore();
        var filters = CreateFilters(store);
        await filters.InitializeAsync();

        filters.MinRentText = "1200000";
        filters.MaxRentText = "3000000";
        filters.MinAreaText = "15";
        filters.MaxAreaText = "35";
        filters.ToggleRoomTypeCommand.Execute(RoomType.BoardingRoom);
        filters.ToggleRoomTypeCommand.Execute(RoomType.MiniApartment);
        filters.SelectMaxDistanceCommand.Execute("3");
        filters.SelectSortCommand.Execute("price_asc");

        var appliedCriteria = new RoomSearchRequest(
            MinRentVnd: 1_200_000,
            MaxRentVnd: 3_000_000,
            MinAreaM2: 15,
            MaxAreaM2: 35,
            RoomTypes: new[] { RoomType.BoardingRoom, RoomType.MiniApartment },
            MaxDistanceKm: 3,
            Sort: "price_asc");
        var expectation = SearchOracle.Search(_dataset, appliedCriteria);

        await WaitUntilAsync(
            () => filters.HasLiveResultCount && !filters.IsCounting && filters.LiveResultCount == expectation.TotalCount,
            "live count reaches the oracle total");

        var landing = CreateLanding(store);
        await landing.InitializeAsync();

        await filters.ApplyCommand.ExecuteAsync(null);
        await WaitForLandingAsync(landing, expectation);

        Assert.Multiple(() =>
        {
            Assert.That(filters.LiveResultCount, Is.EqualTo(landing.TotalRoomCount),
                "US04 AC2/AC3: the live count shown in Filters equals the Explore count after Apply.");
            Assert.That(landing.TotalRoomCount, Is.EqualTo(expectation.TotalCount));
            Assert.That(landing.ActiveFilterCount, Is.EqualTo(5), "price + area + 2 room types + distance");
            Assert.That(landing.HasActiveFilters, Is.True);
            Assert.That(
                landing.ActiveFilterChips.Select(chip => chip.Kind),
                Is.EqualTo(new[]
                {
                    ActiveFilterChipKind.Price,
                    ActiveFilterChipKind.Area,
                    ActiveFilterChipKind.RoomType,
                    ActiveFilterChipKind.RoomType,
                    ActiveFilterChipKind.MaxDistance
                }),
                "Chip order must be Price -> Area -> RoomTypes -> Distance.");
            Assert.That(landing.CurrentSortLabel, Is.EqualTo(AppResources.GetString("Sort_PriceAsc_Short")));
            Assert.That(filters.ShowRoomsText, Is.EqualTo($"Show {expectation.TotalCount} rooms"));
        });
    }

    // ---------------------------------------------------------------------
    // Remove a chip, switch campus through the bridge, clear filters (UC-05 A1/A2, BR-SRCH-06)
    // ---------------------------------------------------------------------

    [Test]
    [CancelAfter(30000)]
    public async Task RemoveChip_CampusBridgeAndClearFilters_FollowRealData()
    {
        var store = new SearchCriteriaStore();
        store.Apply(new RoomSearchCriteria
        {
            MinRentVnd = 1_000_000,
            MaxRentVnd = 3_000_000,
            MinAreaM2 = 15,
            MaxAreaM2 = 35,
            RoomTypes = new[] { RoomType.BoardingRoom, RoomType.MiniApartment },
            MaxDistanceKm = 3,
            Sort = "price_asc"
        });

        var selection = new FakeCampusSelection();
        _ = new CampusSearchBridge(selection, store, SubstituteSession());

        var landing = CreateLanding(store);
        await landing.InitializeAsync();
        await WaitForLandingAsync(landing, SearchOracle.Search(_dataset, store.Current.ToRoomSearchRequest()));
        Assert.That(landing.ActiveFilterChips, Has.Count.EqualTo(5));

        var priceChip = landing.ActiveFilterChips.Single(chip => chip.Kind == ActiveFilterChipKind.Price);
        landing.RemoveFilterChipCommand.Execute(priceChip);

        var withoutPrice = SearchOracle.Search(_dataset, store.Current.ToRoomSearchRequest());
        await WaitForLandingAsync(landing, withoutPrice);

        Assert.Multiple(() =>
        {
            Assert.That(landing.ActiveFilterChips.Select(chip => chip.Kind), Has.None.EqualTo(ActiveFilterChipKind.Price));
            Assert.That(landing.ActiveFilterCount, Is.EqualTo(4));
            Assert.That(store.Current.MinRentVnd, Is.Null);
        });

        await selection.SelectCampusAsync("TNUT");
        Assert.That(store.Current.CampusId, Is.EqualTo("TNUT"),
            "CampusSearchBridge must push the selected campus into the criteria store.");
        var atTnut = SearchOracle.Search(_dataset, store.Current.ToRoomSearchRequest(), "TNUT");
        await WaitForLandingAsync(landing, atTnut);

        await landing.ClearFiltersCommand.ExecuteAsync(null);

        var cleared = SearchOracle.Search(_dataset, store.Current.ToRoomSearchRequest(), "TNUT");
        await WaitForLandingAsync(landing, cleared);

        Assert.Multiple(() =>
        {
            Assert.That(store.Current.CampusId, Is.EqualTo("TNUT"), "BR-SRCH-06: clear keeps the selected campus.");
            Assert.That(store.Current.Sort, Is.Null, "BR-SRCH-06: sort returns to the default (distance).");
            Assert.That(store.Current.HasActiveFilters, Is.False);
            Assert.That(landing.ActiveFilterChips, Is.Empty);
            Assert.That(landing.ActiveFilterCount, Is.Zero);
            Assert.That(landing.TotalRoomCount, Is.EqualTo(52), "Full public list from TNUT (no distance filter).");
        });
    }

    // ---------------------------------------------------------------------
    // Empty state keeps chips and count (US04 AC3)
    // ---------------------------------------------------------------------

    [Test]
    [CancelAfter(30000)]
    public async Task EmptyResults_KeepChipsCountTextAndClearAction()
    {
        var store = new SearchCriteriaStore();
        store.Apply(new RoomSearchCriteria { MaxRentVnd = 99_999 });

        var landing = CreateLanding(store);
        await landing.InitializeAsync();
        await WaitForLandingAsync(landing, SearchOracle.Search(_dataset, store.Current.ToRoomSearchRequest()));

        Assert.Multiple(() =>
        {
            Assert.That(landing.State, Is.EqualTo(ViewState.Empty));
            Assert.That(landing.HasContent, Is.False);
            Assert.That(landing.TotalRoomCount, Is.Zero);
            Assert.That(landing.ResultCountText, Is.EqualTo("0 rooms"));
            Assert.That(landing.HasActiveFilters, Is.True, "US04 AC3: chips stay visible with zero results.");
            Assert.That(landing.ActiveFilterChips, Has.Count.EqualTo(1));
            Assert.That(landing.ActiveFilterChips[0].Kind, Is.EqualTo(ActiveFilterChipKind.Price));
            Assert.That(landing.ClearAllText, Is.Not.Empty);
        });
    }

    // ---------------------------------------------------------------------
    // Debounce and latest-wins reloads (US04 AC2)
    // ---------------------------------------------------------------------

    [Test]
    [CancelAfter(30000)]
    public async Task Debounce_RapidChanges_SendOnlyLatestCriteriaAndFinishUnderTwoSeconds()
    {
        var store = new SearchCriteriaStore();
        var filters = CreateFilters(store, TimeSpan.FromMilliseconds(50));
        await filters.InitializeAsync();

        var stopwatch = Stopwatch.StartNew();
        filters.MaxRentText = "1000000";
        filters.MaxRentText = "1500000";
        filters.MaxRentText = "2000000";
        filters.MaxRentText = "2500000";
        filters.MaxRentText = "3000000";

        var expected = SearchOracle.Search(_dataset, new RoomSearchRequest(MaxRentVnd: 3_000_000));
        await WaitUntilAsync(
            () => filters.HasLiveResultCount && !filters.IsCounting && filters.LiveResultCount == expected.TotalCount,
            "the latest debounced count wins");
        stopwatch.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(filters.LiveResultCount, Is.EqualTo(expected.TotalCount));
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(2000), "US04 AC2: update cycle below 2,000 ms.");
        });
    }

    [Test]
    [CancelAfter(30000)]
    public async Task CriteriaChangedWhileLoading_FinalStateUsesLatestCriteria()
    {
        var store = new SearchCriteriaStore();
        var landing = CreateLanding(store);

        var initialize = landing.InitializeAsync();
        store.Apply(new RoomSearchCriteria { MaxRentVnd = 1_500_000 });
        store.Apply(new RoomSearchCriteria { MaxRentVnd = 2_500_000, RoomTypes = new[] { RoomType.BoardingRoom } });
        await initialize;

        var latest = SearchOracle.Search(_dataset, store.Current.ToRoomSearchRequest());
        await WaitForLandingAsync(landing, latest);

        Assert.Multiple(() =>
        {
            Assert.That(store.Current.MaxRentVnd, Is.EqualTo(2_500_000));
            Assert.That(landing.TotalRoomCount, Is.EqualTo(latest.TotalCount));
            Assert.That(landing.ActiveFilterCount, Is.EqualTo(2));
        });
    }

    // ---------------------------------------------------------------------
    // vi-VN culture over real HTTP (US15 + US04)
    // ---------------------------------------------------------------------

    [Test]
    [CancelAfter(30000)]
    public async Task VietnameseCulture_DecimalCommaInput_StillUsesInvariantQueryAndLocalizesChips()
    {
        LocalizedStrings.Instance.SetCulture(Vi);

        var store = new SearchCriteriaStore();
        var filters = CreateFilters(store, TimeSpan.FromMilliseconds(50));
        await filters.InitializeAsync();

        filters.MinAreaText = "15,5";
        filters.SelectMaxDistanceCommand.Execute("1.5");

        var expected = SearchOracle.Search(_dataset, new RoomSearchRequest(MinAreaM2: 15.5, MaxDistanceKm: 1.5));
        await WaitUntilAsync(
            () => filters.HasLiveResultCount && !filters.IsCounting && filters.LiveResultCount == expected.TotalCount,
            "vi-VN decimal-comma input produces an invariant HTTP query");

        Assert.That(filters.AreaRangeLabel, Does.Contain("15,5"), "Labels use the Vietnamese decimal comma.");

        await filters.ApplyCommand.ExecuteAsync(null);

        var landing = CreateLanding(store);
        await landing.InitializeAsync();
        await WaitForLandingAsync(landing, expected);

        var distanceChip = landing.ActiveFilterChips.Single(chip => chip.Kind == ActiveFilterChipKind.MaxDistance);

        Assert.Multiple(() =>
        {
            Assert.That(store.Current.MinAreaM2, Is.EqualTo(15.5));
            Assert.That(distanceChip.Label, Does.Contain("1,5"));
            Assert.That(landing.ResultCountText, Does.Contain("phòng"));
            Assert.That(landing.TotalRoomCount, Is.EqualTo(expected.TotalCount));
        });
    }

    // ---------------------------------------------------------------------

    private FiltersPageModel CreateFilters(ISearchCriteriaStore store, TimeSpan? debounce = null) =>
        new(_gateway, store, Substitute.For<INavigationService>(), debounce ?? TimeSpan.FromMilliseconds(50));

    private MainLandingPageModel CreateLanding(ISearchCriteriaStore store) =>
        new(_gateway, Substitute.For<INavigationService>(), store);

    private IAppSession SubstituteSession() => Substitute.For<IAppSession>();

    private async Task WaitForLandingAsync(MainLandingPageModel landing, SearchExpectation expectation)
    {
        var expectedIds = expectation.Page(1, 20).Select(match => match.Id).ToList();
        await WaitUntilAsync(
            () => landing.TotalRoomCount == expectation.TotalCount &&
                  landing.Rooms.Select(room => room.Id).SequenceEqual(expectedIds),
            $"landing shows {expectation.TotalCount} rooms in oracle order");
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

    private sealed class FakeCampusSelection : ICampusSelectionService
    {
        public string SelectedCampusId { get; private set; } = "ICTU";

        public CampusDto? SelectedCampus { get; private set; } = new("ICTU", "ICTU", "ICTU", 21.5851, 105.8066, true);

        public event EventHandler? SelectedCampusChanged;

        public IReadOnlyList<CampusDto>? CachedCampuses => null;

        public Task<IReadOnlyList<CampusDto>> GetCampusesAsync(bool forceRefresh = false, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CampusDto>>(Array.Empty<CampusDto>());

        public Task SelectCampusAsync(string campusId, CancellationToken ct = default)
        {
            SelectedCampusId = campusId;
            SelectedCampus = new CampusDto(campusId, campusId, campusId, 0, 0, campusId == "ICTU");
            SelectedCampusChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task SynchronizeAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
