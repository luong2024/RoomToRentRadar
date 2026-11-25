using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using NUnit.Framework;
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

[TestFixture]
public class MainLandingPageModelTests
{
    private static readonly CultureInfo En = new("en-US");

    private IRoomGateway _roomGateway = null!;
    private INavigationService _navigation = null!;

    [SetUp]
    public void SetUp()
    {
        LocalizedStrings.Instance.SetCulture(En);
        _roomGateway = Substitute.For<IRoomGateway>();
        _navigation = Substitute.For<INavigationService>();
    }

    [TearDown]
    public void TearDown()
    {
        LocalizedStrings.Instance.SetCulture(En);
    }

    private MainLandingPageModel CreateModel() => new(_roomGateway, _navigation);

    private static RoomCardDto Room(string id = "room-1") => new(
        id,
        "Sunny studio near ICTU",
        2_500_000,
        25,
        RoomType.BoardingRoom,
        1.2,
        true,
        ListingStatus.Available,
        null,
        21.5851,
        105.8066,
        DateTime.UtcNow,
        false);

    private void GatewayReturnsRooms(params RoomCardDto[] rooms) =>
        _roomGateway.SearchRoomsAsync(Arg.Any<RoomSearchRequest>())
            .Returns(new PagedResult<RoomCardDto>(rooms, rooms.Length, 1, 20));

    private void GatewayThrows(ApiException exception) =>
        _roomGateway.SearchRoomsAsync(Arg.Any<RoomSearchRequest>())
            .Returns(Task.FromException<PagedResult<RoomCardDto>>(exception));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDroppedModel() => new(new MainLandingPageModel(
        Substitute.For<IRoomGateway>(),
        Substitute.For<INavigationService>()));

    [Test]
    public void LocalizationSubscription_DroppedModel_IsCollectableAndLanguageChangeDoesNotThrow()
    {
        var weak = CreateDroppedModel();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.That(weak.IsAlive, Is.False);
        Assert.DoesNotThrow(() => LocalizedStrings.Instance.SetCulture(new CultureInfo("vi-VN")));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CultureChangeToVi_LiveModel_UpdatesExploreTextsAndRemapsError(bool hasError)
    {
        if (hasError)
            GatewayThrows(new ApiException("Server failure", HttpStatusCode.NotFound, "ROOM_NOT_FOUND"));
        else
            GatewayReturnsRooms();

        var model = CreateModel();
        await model.InitializeAsync();
        var expectedError = hasError ? AppResources.GetString("Error_ROOM_NOT_FOUND", new CultureInfo("vi-VN")) : string.Empty;
        var notifications = new List<string?>();
        string? errorWhenExploreTitleChanged = null;
        model.PropertyChanged += (_, e) =>
        {
            notifications.Add(e.PropertyName);
            if (e.PropertyName == nameof(model.ExploreErrorTitleText))
                errorWhenExploreTitleChanged = model.ErrorMessage;
        };

        LocalizedStrings.Instance.SetCulture(new CultureInfo("vi-VN"));

        Assert.That(notifications.Count(name => name == nameof(model.ExploreErrorTitleText)), Is.EqualTo(1));
        Assert.That(notifications.Count(name => name == nameof(model.ExploreEmptyTitleText)), Is.EqualTo(1));
        Assert.That(notifications.Count(name => name == nameof(model.ExploreEmptyMessageText)), Is.EqualTo(1));
        Assert.That(notifications.Count(name => name == nameof(model.ExploreEmptyActionText)), Is.EqualTo(1));
        Assert.That(model.ExploreErrorTitleText, Is.EqualTo(AppResources.GetString("Explore_Error_Title")));
        Assert.That(model.ExploreEmptyTitleText, Is.EqualTo(AppResources.GetString("Explore_Empty_Title")));
        Assert.That(model.ExploreEmptyMessageText, Is.EqualTo(AppResources.GetString("Explore_Empty_Message")));
        Assert.That(model.ExploreEmptyActionText, Is.EqualTo(AppResources.GetString("Explore_Empty_Action")));
        Assert.That(model.ErrorMessage, Is.EqualTo(expectedError));
        Assert.That(errorWhenExploreTitleChanged, Is.EqualTo(expectedError));
    }

    [Test]
    public async Task InitializeAsync_GatewayReturnsRooms_StateContentWithRooms()
    {
        GatewayReturnsRooms(Room());
        var model = CreateModel();

        await model.InitializeAsync();

        Assert.That(model.State, Is.EqualTo(ViewState.Content));
        Assert.That(model.HasContent, Is.True);
        Assert.That(model.Rooms, Has.Count.EqualTo(1));
        var card = model.Rooms[0];
        Assert.That(card.Title, Is.EqualTo("Sunny studio near ICTU"));
        Assert.That(card.PricePerMonth, Is.EqualTo(2_500_000m));
        Assert.That(card.AreaSquareMeters, Is.EqualTo(25));
        Assert.That(card.DistanceMeters, Is.EqualTo(1200).Within(0.001));
    }

    [Test]
    public async Task InitializeAsync_ReturnsEmptyPage_StateEmpty()
    {
        GatewayReturnsRooms();
        var model = CreateModel();

        await model.InitializeAsync();

        Assert.That(model.State, Is.EqualTo(ViewState.Empty));
        Assert.That(model.HasContent, Is.False);
        Assert.That(model.Rooms, Is.Empty);
    }

    [Test]
    public async Task InitializeAsync_ApiException500_StateErrorFallbackMessage()
    {
        GatewayThrows(new ApiException("Server failure", HttpStatusCode.InternalServerError, "INTERNAL_ERROR"));
        var model = CreateModel();

        await model.InitializeAsync();

        Assert.That(model.State, Is.EqualTo(ViewState.Error));
        Assert.That(model.ErrorMessage, Is.EqualTo("An unexpected error occurred, please try again."));
    }

    [Test]
    public async Task InitializeAsync_NetworkError_StateErrorNoInternet()
    {
        GatewayThrows(new ApiException("Network unreachable", (HttpStatusCode)0, ApiErrorCodes.NetworkError));
        var model = CreateModel();

        await model.InitializeAsync();

        Assert.That(model.State, Is.EqualTo(ViewState.Error));
        Assert.That(model.ErrorMessage, Is.EqualTo("No internet connection."));
    }

    [Test]
    public async Task RefreshAfterContent_NetworkError_StateOfflineAndRoomsKept()
    {
        GatewayReturnsRooms(Room());
        var model = CreateModel();
        await model.InitializeAsync();

        GatewayThrows(new ApiException("Network unreachable", (HttpStatusCode)0, ApiErrorCodes.NetworkError));
        await model.RefreshCommand.ExecuteAsync(null);

        Assert.That(model.State, Is.EqualTo(ViewState.Offline));
        Assert.That(model.HasContent, Is.True);
        Assert.That(model.Rooms, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ClearFilters_Invoked_ReloadsWithDefaultRequest()
    {
        GatewayReturnsRooms(Room());
        var model = CreateModel();
        await model.InitializeAsync();

        await model.ClearFiltersCommand.ExecuteAsync(null);

        await _roomGateway.Received(2).SearchRoomsAsync(Arg.Is<RoomSearchRequest>(r => r == new RoomSearchRequest()));
    }

    [Test]
    public async Task GatewayCalledWithDefaultRequestAndPageSize20()
    {
        GatewayReturnsRooms(Room());
        var model = CreateModel();

        await model.InitializeAsync();

        await _roomGateway.Received(1).SearchRoomsAsync(Arg.Is<RoomSearchRequest>(r =>
            r.PageSize == 20 &&
            r.PageNumber == 1 &&
            r.CampusId == null &&
            r.MinRentVnd == null &&
            r.MaxRentVnd == null &&
            r.Sort == null));
    }

    private MainLandingPageModel CreateModel(ISearchCriteriaStore store) =>
        new(_roomGateway, _navigation, store);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }

    [Test]
    public async Task StoreChange_UpdatesChipsBadgeSortAndReloads()
    {
        var store = new SearchCriteriaStore();
        GatewayReturnsRooms(Room());
        var model = CreateModel(store);
        await model.InitializeAsync();

        store.Apply(new RoomSearchCriteria
        {
            MaxRentVnd = 2_000_000,
            RoomTypes = new[] { RoomType.BoardingRoom },
            MaxDistanceKm = 3,
            Sort = "price_asc"
        });

        await WaitUntilAsync(() => model.ActiveFilterChips.Count == 3);

        Assert.That(model.ActiveFilterCount, Is.EqualTo(3));
        Assert.That(model.HasActiveFilters, Is.True);
        Assert.That(model.ActiveFilterChips.Select(c => c.Label), Is.EqualTo(new[] { "Under 2M", "Boarding room", "≤ 3 km" }));
        Assert.That(model.CurrentSortLabel, Is.EqualTo(AppResources.GetString("Sort_PriceAsc_Short")));

        await WaitUntilAsync(() => _roomGateway.ReceivedCalls().Count() >= 2);
        await _roomGateway.Received(2).SearchRoomsAsync(Arg.Any<RoomSearchRequest>());
        await _roomGateway.Received(1).SearchRoomsAsync(Arg.Is<RoomSearchRequest>(r =>
            r.MaxRentVnd == 2_000_000 &&
            r.Sort == "price_asc" &&
            r.PageSize == 20));
    }

    [Test]
    public async Task RemoveFilterChipCommand_UpdatesStoreAndReloads()
    {
        var store = new SearchCriteriaStore();
        store.Apply(new RoomSearchCriteria
        {
            MaxRentVnd = 2_000_000,
            RoomTypes = new[] { RoomType.BoardingRoom }
        });
        GatewayReturnsRooms(Room());
        var model = CreateModel(store);
        await model.InitializeAsync();

        var priceChip = model.ActiveFilterChips.Single(c => c.Kind == ActiveFilterChipKind.Price);
        model.RemoveFilterChipCommand.Execute(priceChip);

        Assert.That(store.Current.MaxRentVnd, Is.Null);
        Assert.That(store.Current.RoomTypes, Is.EqualTo(new[] { RoomType.BoardingRoom }));
        Assert.That(model.ActiveFilterCount, Is.EqualTo(1));

        await WaitUntilAsync(() => _roomGateway.ReceivedCalls().Count() >= 2);
        await _roomGateway.Received(2).SearchRoomsAsync(Arg.Any<RoomSearchRequest>());
        await _roomGateway.Received(1).SearchRoomsAsync(Arg.Is<RoomSearchRequest>(r =>
            r.MaxRentVnd == null && r.RoomTypes != null && r.RoomTypes.Count == 1));
    }

    [Test]
    public async Task ApplySortCommand_UpdatesStoreAndShortLabel()
    {
        var store = new SearchCriteriaStore();
        GatewayReturnsRooms(Room());
        var model = CreateModel(store);
        await model.InitializeAsync();

        model.ApplySortCommand.Execute("price_desc");

        Assert.That(store.Current.Sort, Is.EqualTo("price_desc"));
        Assert.That(model.CurrentSortLabel, Is.EqualTo(AppResources.GetString("Sort_PriceDesc_Short")));
    }

    [Test]
    public async Task ClearFiltersCommand_WithActiveFilters_ResetsStoreAndReloads()
    {
        var store = new SearchCriteriaStore();
        store.Apply(new RoomSearchCriteria { MaxRentVnd = 2_000_000, MaxDistanceKm = 5 });
        GatewayReturnsRooms(Room());
        var model = CreateModel(store);
        await model.InitializeAsync();

        await model.ClearFiltersCommand.ExecuteAsync(null);

        Assert.That(store.Current.HasActiveFilters, Is.False);
        Assert.That(model.ActiveFilterChips, Is.Empty);
        Assert.That(model.ActiveFilterCount, Is.EqualTo(0));
        await _roomGateway.Received(2).SearchRoomsAsync(Arg.Any<RoomSearchRequest>());
        await _roomGateway.Received(1).SearchRoomsAsync(Arg.Is<RoomSearchRequest>(r => r == new RoomSearchRequest()));
    }

    [Test]
    public async Task TotalRoomCount_UsesGatewayTotalCountNotPageItems()
    {
        _roomGateway.SearchRoomsAsync(Arg.Any<RoomSearchRequest>())
            .Returns(new PagedResult<RoomCardDto>(new[] { Room() }, 128, 1, 20));
        var model = CreateModel();

        await model.InitializeAsync();

        Assert.That(model.TotalRoomCount, Is.EqualTo(128));
        Assert.That(model.ResultCountText, Is.EqualTo("128 rooms"));
        Assert.That(model.VerifiedOnlySuffixText, Is.EqualTo(" · verified only"));
    }

    [Test]
    public async Task OpenFiltersCommand_NavigatesToFiltersRoute()
    {
        GatewayReturnsRooms(Room());
        var model = CreateModel();
        await model.InitializeAsync();

        await model.OpenFiltersCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(AppRoutes.SearchFilters);
    }

    [Test]
    public async Task LanguageSwitch_UpdatesExploreFilterTexts()
    {
        _roomGateway.SearchRoomsAsync(Arg.Any<RoomSearchRequest>())
            .Returns(new PagedResult<RoomCardDto>(new[] { Room() }, 128, 1, 20));
        var model = CreateModel();
        await model.InitializeAsync();

        LocalizedStrings.Instance.SetCulture(new CultureInfo("vi-VN"));

        Assert.That(model.FiltersButtonText, Is.EqualTo("Bộ lọc"));
        Assert.That(model.ClearAllText, Is.EqualTo("Xoá tất cả"));
        Assert.That(model.CurrentSortLabel, Is.EqualTo("Gần nhất"));
        Assert.That(model.ResultCountText, Is.EqualTo("128 phòng"));
        Assert.That(model.VerifiedOnlySuffixText, Is.EqualTo(" · đã xác minh"));
    }

    [Test]
    public async Task CriteriaChanged_WhileLoadInFlight_RerunsWithLatestCriteria()
    {
        var store = new SearchCriteriaStore();
        var firstCall = new TaskCompletionSource<PagedResult<RoomCardDto>>();
        var calls = 0;

        _roomGateway.SearchRoomsAsync(Arg.Any<RoomSearchRequest>())
            .Returns(_ =>
            {
                calls++;
                return calls == 1
                    ? firstCall.Task
                    : Task.FromResult(new PagedResult<RoomCardDto>(new[] { Room("room-2") }, 1, 1, 20));
            });

        var model = CreateModel(store);
        var initialize = model.InitializeAsync();

        await WaitUntilAsync(() => calls == 1);

        store.Apply(new RoomSearchCriteria { MaxRentVnd = 2_000_000 });

        Assert.That(model.HasActiveFilters, Is.True, "Chips update before the in-flight load finishes.");

        firstCall.SetResult(new PagedResult<RoomCardDto>(new[] { Room() }, 1, 1, 20));
        await initialize;

        Assert.That(calls, Is.EqualTo(2));
        Assert.That(model.Rooms.Single().Id, Is.EqualTo("room-2"));
        await _roomGateway.Received(1).SearchRoomsAsync(
            Arg.Is<RoomSearchRequest>(r => r.MaxRentVnd == 2_000_000));
    }

    [Test]
    public async Task EmptyResults_WithActiveFilters_KeepsChipsAndZeroCount()
    {
        var store = new SearchCriteriaStore();
        store.Apply(new RoomSearchCriteria { MaxRentVnd = 2_000_000 });
        GatewayReturnsRooms();
        var model = CreateModel(store);

        await model.InitializeAsync();

        Assert.That(model.State, Is.EqualTo(ViewState.Empty));
        Assert.That(model.HasContent, Is.False);
        Assert.That(model.TotalRoomCount, Is.EqualTo(0));
        Assert.That(model.ResultCountText, Is.EqualTo("0 rooms"));
        Assert.That(model.ActiveFilterChips, Has.Count.EqualTo(1));
        Assert.That(model.HasActiveFilters, Is.True);
    }

    [Test]
    public void DroppedModel_SubscribedToStore_IsCollectable()
    {
        var weak = CreateDroppedModelWithStore();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.That(weak.IsAlive, Is.False);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDroppedModelWithStore()
    {
        var store = new SearchCriteriaStore();
        return new WeakReference(new MainLandingPageModel(
            Substitute.For<IRoomGateway>(),
            Substitute.For<INavigationService>(),
            store));
    }
}