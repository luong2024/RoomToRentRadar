using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using NSubstitute;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Navigation.Core;
using RoomToRentRadar.Services.Search.Core;

namespace RoomToRentRadarUnitTests.PageModels;

[TestFixture]
public class FiltersPageModelTests
{
    private static readonly CultureInfo En = new("en-US");
    private static readonly CultureInfo Vi = new("vi-VN");

    private IRoomGateway _roomGateway = null!;
    private SearchCriteriaStore _store = null!;
    private INavigationService _navigation = null!;

    [SetUp]
    public void SetUp()
    {
        LocalizedStrings.Instance.SetCulture(En);
        _roomGateway = Substitute.For<IRoomGateway>();
        _store = new SearchCriteriaStore();
        _navigation = Substitute.For<INavigationService>();
        GatewayReturns(42);
    }

    [TearDown]
    public void TearDown() => LocalizedStrings.Instance.SetCulture(En);

    private FiltersPageModel CreateModel(TimeSpan? debounce = null) =>
        new(_roomGateway, _store, _navigation, debounce ?? TimeSpan.Zero);

    private void GatewayReturns(int totalCount) =>
        _roomGateway.SearchRoomsAsync(Arg.Any<RoomSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<RoomCardDto>(Array.Empty<RoomCardDto>(), totalCount, 1, 1));

    private void GatewayThrows(ApiException exception) =>
        _roomGateway.SearchRoomsAsync(Arg.Any<RoomSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PagedResult<RoomCardDto>>(exception));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDroppedModel() => new(new FiltersPageModel(
        Substitute.For<IRoomGateway>(),
        new SearchCriteriaStore(),
        Substitute.For<INavigationService>(),
        TimeSpan.Zero));

    [Test]
    public async Task InitializeAsync_LoadsDraftFromStoreAndCountsImmediately()
    {
        _store.Apply(new RoomSearchCriteria
        {
            MaxRentVnd = 2_000_000,
            RoomTypes = new[] { RoomType.BoardingRoom },
            MaxDistanceKm = 3,
            Sort = "price_asc"
        });
        var model = CreateModel();

        await model.InitializeAsync();

        Assert.That(model.MaxRentText, Is.EqualTo("2000000"));
        Assert.That(model.RentRangeLabel, Is.EqualTo("Under 2M"));
        Assert.That(model.IsPresetUnder2MSelected, Is.True);
        Assert.That(model.IsBoardingRoomSelected, Is.True);
        Assert.That(model.IsMiniApartmentSelected, Is.False);
        Assert.That(model.IsDistance3Selected, Is.True);
        Assert.That(model.IsSortPriceAscSelected, Is.True);
        Assert.That(model.LiveResultCount, Is.EqualTo(42));
        Assert.That(model.ShowRoomsText, Is.EqualTo("Show 42 rooms"));

        await _roomGateway.Received(1).SearchRoomsAsync(
            Arg.Is<RoomSearchRequest>(r =>
                r.MaxRentVnd == 2_000_000 &&
                r.PageSize == 1 &&
                r.PageNumber == 1 &&
                r.RoomTypes != null &&
                r.RoomTypes.Count == 1 &&
                r.MaxDistanceKm == 3 &&
                r.Sort == "price_asc"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Apply_CommitsDraftToStoreAndNavigatesBack()
    {
        var model = CreateModel();
        await model.InitializeAsync();

        model.SelectRentPresetCommand.Execute("2000000");
        model.ToggleRoomTypeCommand.Execute(RoomType.MiniApartment);
        model.SelectMaxDistanceCommand.Execute("5");
        model.SelectSortCommand.Execute("price_desc");
        await model.ApplyCommand.ExecuteAsync(null);

        Assert.That(_store.Current.MaxRentVnd, Is.EqualTo(2_000_000));
        Assert.That(_store.Current.MinRentVnd, Is.Null);
        Assert.That(_store.Current.RoomTypes, Is.EqualTo(new[] { RoomType.MiniApartment }));
        Assert.That(_store.Current.MaxDistanceKm, Is.EqualTo(5));
        Assert.That(_store.Current.Sort, Is.EqualTo("price_desc"));
        await _navigation.Received(1).GoBackAsync();
    }

    [Test]
    public async Task Close_DiscardsDraftAndDoesNotTouchStore()
    {
        var model = CreateModel();
        await model.InitializeAsync();

        model.SelectRentPresetCommand.Execute("3000000");
        await model.CloseCommand.ExecuteAsync(null);

        Assert.That(_store.Current.HasActiveFilters, Is.False);
        await _navigation.Received(1).GoBackAsync();
    }

    [Test]
    public async Task Clear_ResetsDraftButKeepsCampus()
    {
        _store.SetCampus("campus-ictu", "ICTU");
        _store.Apply(_store.Current with
        {
            MaxRentVnd = 2_000_000,
            RoomTypes = new[] { RoomType.WholeHouse },
            MaxDistanceKm = 2,
            Sort = "newest"
        });
        var model = CreateModel();
        await model.InitializeAsync();

        await model.ClearCommand.ExecuteAsync(null);

        Assert.That(model.RentRangeLabel, Is.EqualTo("Any"));
        Assert.That(model.IsWholeHouseSelected, Is.False);
        Assert.That(model.IsDistance2Selected, Is.False);
        Assert.That(model.IsSortDistanceSelected, Is.True);
        Assert.That(_store.Current.CampusId, Is.EqualTo("campus-ictu"));
        Assert.That(_store.Current.HasActiveFilters, Is.True, "Clear only edits the draft; the store changes on Apply.");
    }

    [Test]
    public async Task Validation_MinRentAboveMax_ShowsInlineErrorBlocksApiAndDisablesApply()
    {
        // Debounce > 0 so the intermediate ("min only") edit is cancelled before it reaches the API.
        var model = CreateModel(TimeSpan.FromMilliseconds(50));
        await model.InitializeAsync();
        _roomGateway.ClearReceivedCalls();

        model.MinRentText = "4000000";
        model.MaxRentText = "2000000";

        Assert.That(model.PriceErrorMessage, Is.EqualTo(AppResources.GetString("Error_INVALID_PRICE_RANGE")));
        Assert.That(model.HasPriceError, Is.True);
        Assert.That(model.CanApply, Is.False);
        Assert.That(model.ApplyCommand.CanExecute(null), Is.False);

        await Task.Delay(200);
        _roomGateway.DidNotReceive().SearchRoomsAsync(Arg.Any<RoomSearchRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Validation_NonNumericRent_ShowsInlineErrorAndBlocksApi()
    {
        var model = CreateModel();
        await model.InitializeAsync();
        _roomGateway.ClearReceivedCalls();

        model.MaxRentText = "abc";

        Assert.That(model.HasPriceError, Is.True);
        Assert.That(model.CanApply, Is.False);

        await Task.Delay(100);
        _roomGateway.DidNotReceive().SearchRoomsAsync(Arg.Any<RoomSearchRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Debounce_RapidChanges_IssueOneCallWithLatestPayload()
    {
        var model = CreateModel(TimeSpan.FromMilliseconds(40));
        await model.InitializeAsync();
        _roomGateway.ClearReceivedCalls();

        model.MaxRentText = "1000000";
        model.MaxRentText = "2000000";
        model.MaxRentText = "3000000";
        model.MaxRentText = "4000000";
        model.MaxRentText = "5000000";

        await Task.Delay(250);

        await _roomGateway.Received(1).SearchRoomsAsync(
            Arg.Is<RoomSearchRequest>(r => r.MaxRentVnd == 5_000_000 && r.PageSize == 1),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApiError_InvalidPriceRange_ShowsLocalizedInlineMessage()
    {
        GatewayThrows(new ApiException("bad range", HttpStatusCode.BadRequest, ProblemCodes.InvalidPriceRange));
        var model = CreateModel();

        await model.InitializeAsync();

        Assert.That(model.PriceErrorMessage, Is.EqualTo(AppResources.GetString("Error_INVALID_PRICE_RANGE")));
        Assert.That(model.HasNetworkError, Is.False);
    }

    [Test]
    public async Task NetworkError_ShowsBannerAndRetryRecovers()
    {
        GatewayThrows(new ApiException("offline", (HttpStatusCode)0, ApiErrorCodes.NetworkError));
        var model = CreateModel();
        await model.InitializeAsync();

        Assert.That(model.HasNetworkError, Is.True);
        Assert.That(model.NetworkErrorMessage, Is.EqualTo("No internet connection."));

        GatewayReturns(42);
        await model.RetryCountCommand.ExecuteAsync(null);

        Assert.That(model.HasNetworkError, Is.False);
        Assert.That(model.LiveResultCount, Is.EqualTo(42));
    }

    [Test]
    public async Task ZeroResults_ShowsZeroLabelAndStillAllowsApply()
    {
        GatewayReturns(0);
        var model = CreateModel();

        await model.InitializeAsync();

        Assert.That(model.ShowRoomsText, Is.EqualTo("Show 0 rooms"));
        Assert.That(model.CanApply, Is.True);
    }

    [Test]
    public async Task SelectRentPreset_TwiceTogglesOff()
    {
        var model = CreateModel();
        await model.InitializeAsync();

        model.SelectRentPresetCommand.Execute("1500000");
        Assert.That(model.IsPresetUnder1_5MSelected, Is.True);
        Assert.That(model.MaxRentText, Is.EqualTo("1500000"));

        model.SelectRentPresetCommand.Execute("1500000");
        Assert.That(model.IsPresetUnder1_5MSelected, Is.False);
        Assert.That(model.MaxRentText, Is.Empty);
    }

    [Test]
    public async Task SelectMaxDistance_TwiceTogglesOff()
    {
        var model = CreateModel();
        await model.InitializeAsync();

        model.SelectMaxDistanceCommand.Execute("2");
        Assert.That(model.IsDistance2Selected, Is.True);

        model.SelectMaxDistanceCommand.Execute("2");
        Assert.That(model.IsDistance2Selected, Is.False);
    }

    [Test]
    public async Task LanguageSwitch_UpdatesLabelsChipsAndButtonText()
    {
        _store.Apply(new RoomSearchCriteria { MaxRentVnd = 1_500_000 });
        var model = CreateModel();
        await model.InitializeAsync();

        LocalizedStrings.Instance.SetCulture(Vi);

        Assert.That(model.TitleText, Is.EqualTo("Bộ lọc"));
        Assert.That(model.ClearText, Is.EqualTo("Xoá"));
        Assert.That(model.RentRangeLabel, Is.EqualTo("Dưới 1,5 tr"));
        Assert.That(model.ShowRoomsText, Is.EqualTo("Xem 42 phòng"));
    }

    [Test]
    public async Task LanguageSwitch_RemapsInlineValidationMessage()
    {
        var model = CreateModel();
        await model.InitializeAsync();
        model.MinRentText = "4000000";
        model.MaxRentText = "2000000";

        LocalizedStrings.Instance.SetCulture(Vi);

        Assert.That(model.PriceErrorMessage, Is.EqualTo(AppResources.GetString("Error_INVALID_PRICE_RANGE", Vi)));
    }

    [Test]
    public async Task ZeroAreaInputs_DoNotCountAsFilter()
    {
        var model = CreateModel();
        await model.InitializeAsync();

        model.MinAreaText = string.Empty;
        model.MaxAreaText = string.Empty;

        Assert.That(model.AreaRangeLabel, Is.EqualTo("Any"));
        Assert.That(model.HasAreaError, Is.False);
    }

    [Test]
    public void LocalizationSubscription_DroppedModel_IsCollectable()
    {
        var weak = CreateDroppedModel();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.That(weak.IsAlive, Is.False);
        Assert.DoesNotThrow(() => LocalizedStrings.Instance.SetCulture(En));
    }
}
