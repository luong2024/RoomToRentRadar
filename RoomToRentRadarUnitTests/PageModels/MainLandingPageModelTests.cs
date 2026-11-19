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
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadarUnitTests.PageModels;

[TestFixture]
public class MainLandingPageModelTests
{
    private static readonly CultureInfo En = new("en-US");

    private IRoomGateway _roomGateway = null!;
    private ISessionStore _sessionStore = null!;
    private INavigationService _navigation = null!;

    [SetUp]
    public void SetUp()
    {
        LocalizedStrings.Instance.SetCulture(En);
        _roomGateway = Substitute.For<IRoomGateway>();
        _sessionStore = Substitute.For<ISessionStore>();
        _navigation = Substitute.For<INavigationService>();
        _sessionStore.GetCachedUserProfileAsync().Returns((UserProfileDto?)null);
    }

    [TearDown]
    public void TearDown()
    {
        LocalizedStrings.Instance.SetCulture(En);
    }

    private MainLandingPageModel CreateModel() => new(_roomGateway, _sessionStore, _navigation);

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
        Substitute.For<ISessionStore>(),
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
}