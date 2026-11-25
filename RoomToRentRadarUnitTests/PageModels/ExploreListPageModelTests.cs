using System.Globalization;
using NSubstitute;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.PageModels.Base;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Navigation.Core;
using RoomToRentRadar.Services.Search.Core;

namespace RoomToRentRadarUnitTests.PageModels;

/// <summary>T01.2 — paging, navigation and header of the Explore list (US01, BR-SRCH-05).</summary>
[TestFixture]
public class ExploreListPageModelTests
{
    private IRoomGateway _gateway = null!;
    private INavigationService _navigation = null!;
    private SearchCriteriaStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        LocalizedStrings.Instance.SetCulture(new CultureInfo("en-US"));
        _gateway = Substitute.For<IRoomGateway>();
        _navigation = Substitute.For<INavigationService>();
        _store = new SearchCriteriaStore();
    }

    [TearDown]
    public void TearDown() => LocalizedStrings.Instance.SetCulture(new CultureInfo("en-US"));

    private MainLandingPageModel CreateModel() => new(_gateway, _navigation, _store);

    private static RoomCardDto Room(int number) => new(
        $"room-{number}", $"Room {number}", 2_500_000, 25, RoomType.BoardingRoom, 1.2, true,
        ListingStatus.Available, $"http://10.0.2.2:5000/media/{number}.jpg", 21.5851, 105.8066, null, false,
        Furnishing.Full);

    private static PagedResult<RoomCardDto> Page(int pageNumber, int total, params int[] numbers) =>
        new(numbers.Select(Room).ToList(), total, pageNumber, 20);

    private static int[] Range(int from, int count) => Enumerable.Range(from, count).ToArray();

    private void PageReturns(int pageNumber, PagedResult<RoomCardDto> page) =>
        _gateway.SearchRoomsAsync(Arg.Is<RoomSearchRequest>(r => r.PageNumber == pageNumber), Arg.Any<CancellationToken>())
            .Returns(page);

    private void PageFails(int pageNumber) =>
        _gateway.SearchRoomsAsync(Arg.Is<RoomSearchRequest>(r => r.PageNumber == pageNumber), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PagedResult<RoomCardDto>>(
                new ApiException("offline", 0, ApiErrorCodes.NetworkError)));

    private int Requests(int pageNumber) => _gateway.ReceivedCalls()
        .Count(call => call.GetArguments()[0] is RoomSearchRequest request && request.PageNumber == pageNumber);

    private async Task<MainLandingPageModel> LoadTwoPageListAsync()
    {
        PageReturns(1, Page(1, 22, Range(1, 20)));
        PageReturns(2, Page(2, 22, 21, 22));
        var model = CreateModel();
        await model.InitializeAsync();
        return model;
    }

    // ----------------------------------------------------------------- paging

    [Test]
    public async Task LoadMore_AppendsTheNextPage_ThenStops()
    {
        var model = await LoadTwoPageListAsync();

        await model.LoadMoreCommand.ExecuteAsync(null);
        await model.LoadMoreCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.Rooms.Select(r => r.Id), Is.EqualTo(Range(1, 22).Select(n => $"room-{n}")));
            Assert.That(model.TotalRoomCount, Is.EqualTo(22));
            Assert.That(model.IsLoadingMore, Is.False);
            Assert.That(Requests(2), Is.EqualTo(1));
            Assert.That(Requests(3), Is.Zero);
        });
    }

    [Test]
    public async Task LoadMore_SingleShortPage_NeverAsksForMore()
    {
        PageReturns(1, Page(1, 3, 1, 2, 3));
        var model = CreateModel();
        await model.InitializeAsync();

        await model.LoadMoreCommand.ExecuteAsync(null);

        Assert.That(Requests(2), Is.Zero);
    }

    [Test]
    public async Task LoadMore_SkipsRoomsAlreadyShown()
    {
        PageReturns(1, Page(1, 22, Range(1, 20)));
        PageReturns(2, Page(2, 22, 20, 21));
        var model = CreateModel();
        await model.InitializeAsync();

        await model.LoadMoreCommand.ExecuteAsync(null);

        Assert.That(model.Rooms.Select(r => r.Id), Is.Unique);
        Assert.That(model.Rooms, Has.Count.EqualTo(21));
    }

    [Test]
    public async Task LoadMore_Failure_KeepsList_AndOnlyRetryAsksAgain()
    {
        PageReturns(1, Page(1, 22, Range(1, 20)));
        PageFails(2);
        var model = CreateModel();
        await model.InitializeAsync();

        await model.LoadMoreCommand.ExecuteAsync(null);
        await model.LoadMoreCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.IsLoadMoreFailed, Is.True);
            Assert.That(model.Rooms, Has.Count.EqualTo(20));
            Assert.That(model.State, Is.EqualTo(ViewState.Content));
            Assert.That(Requests(2), Is.EqualTo(1));
        });

        PageReturns(2, Page(2, 22, 21, 22));
        await model.RetryLoadMoreCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.IsLoadMoreFailed, Is.False);
            Assert.That(model.Rooms, Has.Count.EqualTo(22));
        });
    }

    [Test]
    public async Task LoadMore_LateResultAfterCriteriaChange_IsDropped()
    {
        var slowPage = new TaskCompletionSource<PagedResult<RoomCardDto>>();
        PageReturns(1, Page(1, 22, Range(1, 20)));
        _gateway.SearchRoomsAsync(Arg.Is<RoomSearchRequest>(r => r.PageNumber == 2), Arg.Any<CancellationToken>())
            .Returns(slowPage.Task);
        var model = CreateModel();
        await model.InitializeAsync();
        var loadingMore = model.LoadMoreCommand.ExecuteAsync(null);

        PageReturns(1, Page(1, 1, 99));
        _store.Apply(new RoomSearchCriteria { MaxRentVnd = 2_000_000 });
        slowPage.SetResult(Page(2, 22, 21, 22));
        await loadingMore;

        Assert.Multiple(() =>
        {
            Assert.That(model.Rooms.Select(r => r.Id), Is.EqualTo(new[] { "room-99" }));
            Assert.That(model.IsLoadingMore, Is.False);
            Assert.That(model.IsLoadMoreFailed, Is.False);
        });
    }

    [Test]
    public async Task LoadMore_WhileAlreadyLoading_IsIgnored()
    {
        var slowPage = new TaskCompletionSource<PagedResult<RoomCardDto>>();
        PageReturns(1, Page(1, 22, Range(1, 20)));
        _gateway.SearchRoomsAsync(Arg.Is<RoomSearchRequest>(r => r.PageNumber == 2), Arg.Any<CancellationToken>())
            .Returns(slowPage.Task);
        var model = CreateModel();
        await model.InitializeAsync();

        var first = model.LoadMoreCommand.ExecuteAsync(null);
        var second = model.LoadMoreCommand.ExecuteAsync(null);
        Assert.That(model.IsLoadingMore, Is.True);
        slowPage.SetResult(Page(2, 22, 21, 22));
        await Task.WhenAll(first, second);

        Assert.That(Requests(2), Is.EqualTo(1));
    }

    [Test]
    public async Task LoadMore_AfterAFailedRefresh_ContinuesTheListOnScreen()
    {
        var model = await LoadTwoPageListAsync();
        PageFails(1);
        await model.RefreshCommand.ExecuteAsync(null);
        Assert.That(model.State, Is.EqualTo(ViewState.Offline));

        await model.LoadMoreCommand.ExecuteAsync(null);

        Assert.That(model.Rooms, Has.Count.EqualTo(22));
    }

    [Test]
    public async Task Refresh_ReplacesTheListWithTheFirstPage()
    {
        var model = await LoadTwoPageListAsync();
        await model.LoadMoreCommand.ExecuteAsync(null);

        await model.RefreshCommand.ExecuteAsync(null);

        Assert.That(model.Rooms, Has.Count.EqualTo(20));
    }

    [Test]
    public async Task Reappearing_WithAnUpToDateList_DoesNotReload()
    {
        var model = await LoadTwoPageListAsync();
        await model.LoadMoreCommand.ExecuteAsync(null);

        await model.InitializeAsync();

        Assert.That(model.Rooms, Has.Count.EqualTo(22));
        Assert.That(Requests(1), Is.EqualTo(1));
    }

    // ----------------------------------------------------------------- load errors

    [Test]
    public async Task FirstLoadFailure_ShowsTheScreenErrorBlock_UntilRetrySucceeds()
    {
        PageFails(1);
        var model = CreateModel();
        var raised = new List<string?>();
        model.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        await model.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(model.IsLoadFailed, Is.True);
            Assert.That(model.IsBodyVisible, Is.False);
            Assert.That(model.ErrorMessage, Is.Not.Empty);
            Assert.That(raised, Does.Contain(nameof(model.IsLoadFailed)));
            Assert.That(raised, Does.Contain(nameof(model.IsBodyVisible)));
        });

        PageReturns(1, Page(1, 1, 1));
        await model.RetryCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.IsLoadFailed, Is.False);
            Assert.That(model.IsBodyVisible, Is.True);
            Assert.That(model.Rooms, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task FailedRefresh_WithAListOnScreen_KeepsTheList()
    {
        var model = await LoadTwoPageListAsync();
        PageFails(1);

        await model.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.State, Is.EqualTo(ViewState.Offline));
            Assert.That(model.IsLoadFailed, Is.False);
            Assert.That(model.IsBodyVisible, Is.True);
            Assert.That(model.Rooms, Has.Count.EqualTo(20));
            Assert.That(model.TotalRoomCount, Is.EqualTo(22));
        });
    }

    // ----------------------------------------------------------------- cards and header

    [Test]
    public async Task Cards_CarryCoverTypeFurnishingAndCampus()
    {
        _store.SetCampus("TNUT", "TNUT");
        PageReturns(1, Page(1, 1, 1));
        var model = CreateModel();

        await model.InitializeAsync();

        var card = model.Rooms.Single();
        Assert.Multiple(() =>
        {
            Assert.That(card.ThumbnailUri, Is.EqualTo(new Uri("http://10.0.2.2:5000/media/1.jpg")));
            Assert.That(card.MetaText, Is.EqualTo("25 m² · Boarding room · Furnished"));
            Assert.That(card.DistanceText, Is.EqualTo("1.2 km to TNUT"));
        });
    }

    [Test]
    public async Task LanguageSwitch_RebuildsCards()
    {
        PageReturns(1, Page(1, 1, 1));
        var model = CreateModel();
        await model.InitializeAsync();
        var english = model.Rooms.Single();

        LocalizedStrings.Instance.SetCulture(new CultureInfo("vi-VN"));

        var vietnamese = model.Rooms.Single();
        Assert.Multiple(() =>
        {
            Assert.That(vietnamese, Is.Not.EqualTo(english));
            Assert.That(vietnamese.PriceText, Is.EqualTo("2,5 tr/tháng"));
            Assert.That(model.NearText, Is.EqualTo("Gần"));
        });
        GC.KeepAlive(model);
    }

    [Test]
    public void CampusChip_FollowsTheStore()
    {
        PageReturns(1, Page(1, 0));
        var model = CreateModel();

        _store.SetCampus("TUMP", "TUMP");

        Assert.That(model.CampusName, Is.EqualTo("TUMP"));
        Assert.That(model.CampusChipDescription, Does.Contain("TUMP"));
        GC.KeepAlive(model);
    }

    [Test]
    public void UnreadDotAndCompareTray_AreHiddenUntilDataExists()
    {
        var model = CreateModel();

        Assert.Multiple(() =>
        {
            Assert.That(model.HasUnreadNotifications, Is.False);
            Assert.That(model.HasCompareSelection, Is.False);
        });

        model.HasUnreadNotifications = true;
        model.CompareCount = 2;

        Assert.Multiple(() =>
        {
            Assert.That(model.HasCompareSelection, Is.True);
            Assert.That(model.CompareCountText, Is.EqualTo("Compare (2)"));
            Assert.That(model.NotificationsDescription, Is.EqualTo("Notifications, unread"));
        });
    }

    // ----------------------------------------------------------------- navigation

    [Test]
    public async Task OpenRoom_PassesRoomAndSelectedCampus()
    {
        _store.SetCampus("TNUT", "TNUT");
        PageReturns(1, Page(1, 1, 1));
        var model = CreateModel();
        await model.InitializeAsync();

        await model.ViewRoomDetailsCommand.ExecuteAsync("room-1");

        await _navigation.Received(1).GoToAsync($"{AppRoutes.RoomDetail}?roomId=room-1&campusId=TNUT");
    }

    [TestCase(null)]
    [TestCase("")]
    public async Task OpenRoom_WithoutId_DoesNothing(string? roomId)
    {
        var model = CreateModel();

        await model.ViewRoomDetailsCommand.ExecuteAsync(roomId);

        await _navigation.DidNotReceive().GoToAsync(Arg.Any<string>());
    }

    [Test]
    public async Task HeaderActions_OpenTheirScreens()
    {
        var model = CreateModel();

        await model.OpenCampusCommand.ExecuteAsync(null);
        await model.OpenMapCommand.ExecuteAsync(null);
        await model.OpenCompareCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(AppRoutes.AccountCampus, null);
        await _navigation.Received(1).GoToTabAsync(AppRoutes.Map);
        await _navigation.Received(1).GoToAsync(AppRoutes.Compare, null);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task NotificationsAndSaveSearch_RequireSignIn(bool signedIn)
    {
        _navigation.RequireSignInAsync(Arg.Any<string>()).Returns(signedIn);
        var model = CreateModel();

        await model.OpenNotificationsCommand.ExecuteAsync(null);
        await model.SaveSearchCommand.ExecuteAsync(null);

        await _navigation.Received(2).RequireSignInAsync("//explore");
        await _navigation.Received(signedIn ? 1 : 0).GoToAsync(AppRoutes.Notifications, null);
        await _navigation.Received(signedIn ? 1 : 0).GoToAsync(AppRoutes.SavedSearches, null);
    }
}
