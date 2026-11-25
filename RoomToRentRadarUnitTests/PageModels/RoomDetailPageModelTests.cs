using System.Globalization;
using System.Net;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadarUnitTests.PageModels;

[TestFixture]
public class RoomDetailPageModelTests
{
    private IRoomGateway _roomGateway = null!;
    private ISessionStore _sessionStore = null!;
    private IFavouriteGateway _favouriteGateway = null!;
    private RoomDetailPageModel _sut = null!;

    [SetUp]
    public void SetUp()
    {
        LocalizedStrings.Instance.SetCulture(new CultureInfo("en-US"));

        _roomGateway = Substitute.For<IRoomGateway>();
        _sessionStore = Substitute.For<ISessionStore>();
        _favouriteGateway = Substitute.For<IFavouriteGateway>();

        _sessionStore.HasValidTokenAsync().Returns(false);
        _sessionStore.GetCachedUserProfileAsync().Returns((UserProfileDto?)null);

        _sut = new RoomDetailPageModel(_roomGateway, _sessionStore, _favouriteGateway)
        {
            RoomId = "room-test-1",
            CampusId = "ictu",
            CampusName = "ICTU"
        };
    }

    [TearDown]
    public void TearDown()
    {
        _sut?.Dispose();
        LocalizedStrings.Instance.SetCulture(new CultureInfo("en-US"));
    }

    [Test]
    public async Task InitializeAsync_WhenRoomIsAvailable_PopulatesDetailsAndAllSections()
    {
        // Arrange
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: new DateOnly(2026, 12, 1),
            distanceKm: 1.2,
            landlordPhone: null); // Hidden for guests (BR-CNT-01)

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));

        // Act
        await _sut.InitializeAsync();

        // Assert - Basic State
        Assert.That(_sut.IsLoading, Is.False);
        Assert.That(_sut.HasError, Is.False);
        Assert.That(_sut.IsNotFound, Is.False);
        Assert.That(_sut.IsContentVisible, Is.True);
        Assert.That(_sut.IsAvailable, Is.True);
        Assert.That(_sut.IsUnavailable, Is.False);

        // Assert - Header & Pricing
        Assert.That(_sut.Title, Is.EqualTo("Private room with balcony near ICTU"));
        Assert.That(_sut.PriceFormatted, Is.EqualTo("2,500,000 đ"));
        Assert.That(_sut.PeriodFormatted, Is.EqualTo("/ month"));
        Assert.That(_sut.DepositFormatted, Is.EqualTo("Deposit 2,500,000 đ (1 month)"));
        Assert.That(_sut.StatusBadgeText, Is.EqualTo("Verified · Available"));

        // Assert - Key Facts
        Assert.That(_sut.KeyFacts, Has.Count.EqualTo(6));
        Assert.That(_sut.KeyFacts[0].Value, Is.EqualTo("25 m²"));
        Assert.That(_sut.KeyFacts[1].Value, Is.EqualTo("Boarding room"));
        Assert.That(_sut.KeyFacts[2].Value, Is.EqualTo("2 people"));
        Assert.That(_sut.KeyFacts[3].Value, Is.EqualTo("Full"));
        Assert.That(_sut.KeyFacts[4].Value, Is.EqualTo("1 Dec"));
        Assert.That(_sut.KeyFacts[5].Value, Is.EqualTo("1.2 km"));

        // Assert - Monthly Costs (5 breakdown rows + estimated total)
        Assert.That(_sut.CostBreakdownRows, Has.Count.EqualTo(5));
        Assert.That(_sut.CostBreakdownRows[0].ValueFormatted, Is.EqualTo("2,500,000 đ")); // Rent
        Assert.That(_sut.CostBreakdownRows[1].ValueFormatted, Is.EqualTo("3,500 đ / kWh")); // Electricity
        Assert.That(_sut.CostBreakdownRows[2].ValueFormatted, Is.EqualTo("100,000 đ / person")); // Water
        Assert.That(_sut.CostBreakdownRows[3].ValueFormatted, Is.EqualTo("Included")); // Internet
        Assert.That(_sut.CostBreakdownRows[4].ValueFormatted, Is.EqualTo("50,000 đ")); // Parking
        Assert.That(_sut.EstimatedTotalFormatted, Is.EqualTo("~2.95M/mo"));

        // Assert - Amenities (11 provided, top 8 displayed initially)
        Assert.That(_sut.AmenitiesDisplayList, Has.Count.EqualTo(8));
        Assert.That(_sut.HasMoreAmenities, Is.True);
        Assert.That(_sut.SeeAllAmenitiesText, Is.EqualTo("See all 11 amenities"));

        // Assert - Safety
        Assert.That(_sut.SafetyScoreFormatted, Is.EqualTo("4/5 · Verified"));
        Assert.That(_sut.SafetyChecklist, Has.Count.EqualTo(5));

        // Assert - House Rules
        Assert.That(_sut.HouseRulesList, Has.Count.EqualTo(5));
        Assert.That(_sut.HouseRulesList[0].Title, Is.EqualTo("Quiet hours"));
        Assert.That(_sut.HouseRulesList[0].Detail, Is.EqualTo("22:00 – 06:00"));
        Assert.That(_sut.HouseRulesList[1].Title, Is.EqualTo("Curfew"));
        Assert.That(_sut.HouseRulesList[1].Detail, Is.EqualTo("23:30 (gate closes)"));
        Assert.That(_sut.HouseRulesList[2].Title, Is.EqualTo("Guests"));
        Assert.That(_sut.HouseRulesList[2].Detail, Is.EqualTo("Daytime only"));
        Assert.That(_sut.HouseRulesList[3].Title, Is.EqualTo("Cooking"));
        Assert.That(_sut.HouseRulesList[3].Detail, Is.EqualTo("Shared kitchen only"));
        Assert.That(_sut.HouseRulesList[4].Title, Is.EqualTo("Parking"));
        Assert.That(_sut.HouseRulesList[4].Detail, Is.EqualTo("Motorbikes in the yard, one per room"));

        // Assert - Commute Times
        Assert.That(_sut.CommuteTimes, Has.Count.EqualTo(3));
        Assert.That(_sut.HasTravelTimes, Is.True);
        Assert.That(_sut.IsCommuteFallback, Is.False);
        Assert.That(_sut.CommuteTimes[0].Mode, Is.EqualTo(TravelMode.Motorbike));
        Assert.That(_sut.CommuteTimes[0].TimeFormatted, Is.EqualTo("8 mins"));
        Assert.That(_sut.CommuteTimes[0].DistanceFormatted, Is.EqualTo("1.9 km"));
        Assert.That(_sut.CommuteTimes[1].Mode, Is.EqualTo(TravelMode.Bus));
        Assert.That(_sut.CommuteTimes[1].TimeFormatted, Is.EqualTo("~19 mins"));
        Assert.That(_sut.CommuteTimes[2].Mode, Is.EqualTo(TravelMode.Walking));
        Assert.That(_sut.CommuteTimes[2].TimeFormatted, Is.EqualTo("25 mins"));
        Assert.That(_sut.CommuteTimes[2].DistanceFormatted, Is.EqualTo("1.5 km"));

        // Assert - Landlord Contact Privacy for Guest (AC4 & BR-CNT-01)
        Assert.That(_sut.IsSignedIn, Is.False);
        Assert.That(_sut.LandlordName, Is.EqualTo("Nguyễn Thị Hoa"));
        Assert.That(_sut.LandlordInitials, Is.EqualTo("NH"));
        Assert.That(_sut.LandlordPhoneNumber, Is.Null);
        Assert.That(_sut.CanCall, Is.False);
    }

    [Test]
    public async Task InitializeAsync_WhenRoomIsUnavailable_LoadsSimilarRoomsAndSetsUnavailableBanner()
    {
        // Arrange (SCR-ROOM-01-UNAVAILABLE: Marked rented)
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Rented,
            isVerified: true,
            monthlyRent: 1800000,
            deposit: 1800000,
            areaM2: 18.0,
            maxOccupants: 1,
            furnishing: Furnishing.Basic,
            availableFrom: null,
            distanceKm: 1.6,
            landlordPhone: null);

        var similarRooms = new List<RoomCardDto>
        {
            new("sim-1", "Room near Tân Thịnh market", 1900000, 20.0, RoomType.BoardingRoom, 0.4, true, ListingStatus.Available, null, 21.5, 105.8, DateTime.UtcNow, false),
            new("sim-2", "Boarding room, Bắc Sơn street", 1700000, 18.0, RoomType.BoardingRoom, 0.9, false, ListingStatus.Available, null, 21.5, 105.8, DateTime.UtcNow, false)
        };

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));
        _roomGateway.GetSimilarRoomsAsync("room-test-1", "ictu").Returns(similarRooms);

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.That(_sut.IsAvailable, Is.False);
        Assert.That(_sut.IsUnavailable, Is.True);
        Assert.That(_sut.UnavailableBannerText, Does.Contain("This room is no longer available. It was marked as rented"));
        Assert.That(_sut.ContactDisabledText, Is.EqualTo("Contact is disabled for unavailable rooms"));
        Assert.That(_sut.SimilarRooms, Has.Count.EqualTo(2));
        Assert.That(_sut.SimilarRooms[0].Title, Is.EqualTo("Room near Tân Thịnh market"));
        Assert.That(_sut.SimilarRooms[0].PriceFormatted, Is.EqualTo("1.9M/mo"));
        Assert.That(_sut.SimilarRooms[0].DistanceFormatted, Is.EqualTo("0.4 km from this room"));
    }

    [Test]
    public async Task InitializeAsync_WhenRoomNotFound_SetsIsNotFound()
    {
        // Arrange
        _roomGateway.GetRoomDetailAsync("missing-id", Arg.Any<string?>())
            .ThrowsAsync(new ApiException("Room not found", HttpStatusCode.NotFound));

        _sut.RoomId = "missing-id";

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.That(_sut.IsNotFound, Is.True);
        Assert.That(_sut.HasError, Is.False);
        Assert.That(_sut.IsLoading, Is.False);
        Assert.That(_sut.IsContentVisible, Is.False);
        Assert.That(_sut.ErrorMessage, Is.EqualTo(AppResources.GetString("RoomDetail_NotFoundMessage")));
    }

    [Test]
    public async Task InitializeAsync_WhenGatewayThrowsGeneralException_SetsHasError()
    {
        // Arrange
        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu")
            .ThrowsAsync(new InvalidOperationException("Network connection failed"));

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.That(_sut.HasError, Is.True);
        Assert.That(_sut.IsNotFound, Is.False);
        Assert.That(_sut.IsLoading, Is.False);
        Assert.That(_sut.IsContentVisible, Is.False);
        Assert.That(_sut.ErrorMessage, Does.Contain("Network connection failed"));
    }

    [Test]
    public async Task InitializeAsync_WhenUserIsSignedIn_RevealsLandlordPhone()
    {
        // Arrange
        _sessionStore.HasValidTokenAsync().Returns(true);

        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: "0912345678");

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.That(_sut.IsSignedIn, Is.True);
        Assert.That(_sut.LandlordPhoneNumber, Is.EqualTo("0912345678"));
        Assert.That(_sut.CanCall, Is.True);
        Assert.That(_sut.CanZalo, Is.True);
        Assert.That(_sut.LandlordZaloUrl, Is.EqualTo("https://zalo.me/0912345678"));
    }

    [Test]
    public async Task ToggleAllAmenities_ExpandsToAllAndCollapsesToEight()
    {
        // Arrange
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: null);

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));
        await _sut.InitializeAsync();

        Assert.That(_sut.AmenitiesDisplayList, Has.Count.EqualTo(8));
        Assert.That(_sut.IsAllAmenitiesExpanded, Is.False);

        // Act 1 - Expand
        _sut.ToggleAllAmenities();

        // Assert 1
        Assert.That(_sut.IsAllAmenitiesExpanded, Is.True);
        Assert.That(_sut.AmenitiesDisplayList, Has.Count.EqualTo(11));
        Assert.That(_sut.SeeAllAmenitiesText, Is.EqualTo("Collapse amenities"));

        // Act 2 - Collapse
        _sut.ToggleAllAmenities();

        // Assert 2
        Assert.That(_sut.IsAllAmenitiesExpanded, Is.False);
        Assert.That(_sut.AmenitiesDisplayList, Has.Count.EqualTo(8));
        Assert.That(_sut.SeeAllAmenitiesText, Is.EqualTo("See all 11 amenities"));
    }

    [Test]
    public async Task CallLandlordAsync_WhenGuest_PromptsSignInAndNavigatesToLogin()
    {
        // Arrange
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: null);

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));
        await _sut.InitializeAsync();

        bool alertPrompted = false;
        string? navigatedRoute = null;

        _sut.DisplayAlertOverride = (title, message, accept, cancel) =>
        {
            alertPrompted = true;
            return Task.FromResult(true); // User clicks "Sign In"
        };
        _sut.NavigateOverride = route =>
        {
            navigatedRoute = route;
            return Task.CompletedTask;
        };

        // Act
        await _sut.CallLandlordAsync();

        // Assert
        Assert.That(alertPrompted, Is.True);
        Assert.That(navigatedRoute, Is.EqualTo("//login"));
    }

    [Test]
    public async Task CallLandlordAsync_WhenSignedIn_InvokesPhoneDialer()
    {
        // Arrange
        _sessionStore.HasValidTokenAsync().Returns(true);

        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: "0912345678");

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));
        await _sut.InitializeAsync();

        string? dialedPhone = null;
        _sut.PhoneDialerOverride = phone => dialedPhone = phone;

        // Act
        await _sut.CallLandlordAsync();

        // Assert
        Assert.That(dialedPhone, Is.EqualTo("0912345678"));
    }

    [Test]
    public async Task OpenZaloAsync_WhenSignedIn_InvokesLauncherWithZaloUrl()
    {
        // Arrange
        _sessionStore.HasValidTokenAsync().Returns(true);

        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: "0912345678");

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));
        await _sut.InitializeAsync();

        string? launchedUrl = null;
        _sut.LauncherOverride = url =>
        {
            launchedUrl = url;
            return Task.CompletedTask;
        };

        // Act
        await _sut.OpenZaloAsync();

        // Assert
        Assert.That(launchedUrl, Is.EqualTo("https://zalo.me/0912345678"));
    }

    [Test]
    public async Task ToggleFavouriteAsync_WhenSignedIn_TogglesStateAndCallsGateway()
    {
        // Arrange
        _sessionStore.HasValidTokenAsync().Returns(true);

        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: "0912345678");

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));
        await _sut.InitializeAsync();

        Assert.That(_sut.IsFavourite, Is.False);

        // Act 1 - Add favourite
        await _sut.ToggleFavouriteAsync();

        // Assert 1
        Assert.That(_sut.IsFavourite, Is.True);
        await _favouriteGateway.Received(1).AddFavouriteAsync("room-test-1");

        // Act 2 - Remove favourite
        await _sut.ToggleFavouriteAsync();

        // Assert 2
        Assert.That(_sut.IsFavourite, Is.False);
        await _favouriteGateway.Received(1).RemoveFavouriteAsync("room-test-1");
    }

    [Test]
    public async Task ToggleFavouriteAsync_WhenGatewayThrows_RollsBackState()
    {
        // Arrange
        _sessionStore.HasValidTokenAsync().Returns(true);

        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: "0912345678");

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));
        await _sut.InitializeAsync();

        _favouriteGateway.AddFavouriteAsync("room-test-1")
            .ThrowsAsync(new InvalidOperationException("Gateway timeout"));

        // Act
        await _sut.ToggleFavouriteAsync();

        // Assert - Rolled back to false
        Assert.That(_sut.IsFavourite, Is.False);
    }

    [Test]
    public async Task SelectSimilarRoomAsync_NavigatesToRoomDetailRoute()
    {
        // Arrange
        string? targetRoute = null;
        _sut.NavigateOverride = route =>
        {
            targetRoute = route;
            return Task.CompletedTask;
        };

        // Act
        await _sut.SelectSimilarRoomAsync("sim-999");

        // Assert
        Assert.That(targetRoute, Is.EqualTo("roomdetail?roomId=sim-999&campusId=ictu"));
    }

    [Test]
    public async Task ReportListingAsync_NavigatesToReportRoute()
    {
        // Arrange
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: null);

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));
        await _sut.InitializeAsync();

        string? targetRoute = null;
        _sut.NavigateOverride = route =>
        {
            targetRoute = route;
            return Task.CompletedTask;
        };

        // Act
        await _sut.ReportListingAsync();

        // Assert
        Assert.That(targetRoute, Is.EqualTo("report?roomId=room-test-1"));
    }

    [Test]
    public async Task CultureChange_ToVietnamese_UpdatesAllLocalizedStrings()
    {
        // Arrange
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.2,
            landlordPhone: null);

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));
        await _sut.InitializeAsync();

        Assert.That(_sut.StatusBadgeText, Is.EqualTo("Verified · Available"));

        // Act - Switch culture to Vietnamese
        LocalizedStrings.Instance.SetCulture(new CultureInfo("vi-VN"));

        // Assert
        Assert.That(_sut.StatusBadgeText, Is.EqualTo("Đã xác minh · Còn phòng"));
        Assert.That(_sut.PeriodFormatted, Is.EqualTo("/ tháng"));
        Assert.That(_sut.KeyFacts[0].Label, Is.EqualTo("Diện tích"));
        Assert.That(_sut.KeyFacts[1].Label, Is.EqualTo("Loại phòng"));
        Assert.That(_sut.HouseRulesList[0].Title, Is.EqualTo("Giờ yên tĩnh"));
        Assert.That(_sut.HouseRulesList[1].Title, Is.EqualTo("Giờ giới nghiêm"));
    }

    [Test]
    public async Task PopulateMonthlyCosts_WithVariousUnits_FormatsWaterAndElectricityCorrectly()
    {
        // Arrange
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2000000,
            deposit: 2000000,
            areaM2: 20.0,
            maxOccupants: 1,
            furnishing: Furnishing.Basic,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: null) with
        {
            Utilities = new UtilitiesView(
                ElectricityPriceVndPerKwh: null,
                WaterPriceVnd: 15000,
                WaterPriceUnit: WaterPriceUnit.PerCubicMeter,
                InternetFeeVnd: 50000,
                ParkingFeeVnd: null,
                OtherFeesNote: null,
                EstimatedMonthlyTotalVnd: 2200000)
        };

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.That(_sut.CostBreakdownRows[1].ValueFormatted, Is.EqualTo("Included")); // Electricity null -> Included
        Assert.That(_sut.CostBreakdownRows[2].ValueFormatted, Is.EqualTo("15,000 đ / m³")); // Water per m3
        Assert.That(_sut.CostBreakdownRows[3].ValueFormatted, Is.EqualTo("50,000 đ")); // Internet
        Assert.That(_sut.CostBreakdownRows[4].ValueFormatted, Is.EqualTo("Free")); // Parking null -> Free
    }

    [Test]
    public async Task PopulateSafety_WithUnverifiedAndPartialFeatures_FormatsScoreAndChecklist()
    {
        // Arrange
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: false,
            monthlyRent: 2000000,
            deposit: 2000000,
            areaM2: 20.0,
            maxOccupants: 1,
            furnishing: Furnishing.Basic,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: null) with
        {
            Safety = new SafetyDto(2, false, [SafetyFeature.Cctv, SafetyFeature.FireSafety])
        };

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.That(_sut.SafetyScoreFormatted, Is.EqualTo("2/5 · Unverified"));
        Assert.That(_sut.SafetyChecklist, Has.Count.EqualTo(5));
        Assert.That(_sut.SafetyChecklist[0].IsVerified, Is.True); // CCTV
        Assert.That(_sut.SafetyChecklist[1].IsVerified, Is.False); // Secure entrance
        Assert.That(_sut.SafetyChecklist[2].IsVerified, Is.True); // Fire safety
    }

    [Test]
    public async Task PopulateHouseRules_WhenNullOrPartial_HandlesGracefully()
    {
        // Arrange
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2000000,
            deposit: 2000000,
            areaM2: 20.0,
            maxOccupants: 1,
            furnishing: Furnishing.Basic,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: null) with
        {
            HouseRules = null
        };

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.That(_sut.HasHouseRules, Is.False);
        Assert.That(_sut.HouseRulesList, Is.Empty);
    }

    [Test]
    public async Task HeroGallery_PhotoNavigationAnd360Indicator()
    {
        // Arrange
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2000000,
            deposit: 2000000,
            areaM2: 20.0,
            maxOccupants: 1,
            furnishing: Furnishing.Basic,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: null);

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.That(_sut.HasPhotos, Is.True);
        Assert.That(_sut.Is360Available, Is.True);
        Assert.That(_sut.PhotoCounterText, Is.EqualTo("1 / 2"));
        Assert.That(_sut.HeroCoverUrl, Is.EqualTo("orig1.jpg"));
    }

    [Test]
    public async Task Open360ViewAsync_WhenTapped_InvokesToastOverride()
    {
        // Arrange
        string? toastMessage = null;
        _sut.ToastOverride = msg =>
        {
            toastMessage = msg;
            return Task.CompletedTask;
        };

        // Act
        await _sut.Open360ViewAsync();

        // Assert
        Assert.That(toastMessage, Is.EqualTo(AppResources.GetString("RoomDetail_360View")));
    }

    [Test]
    public async Task ShareListingAsync_WhenTapped_InvokesShareOverrideWithTitleAndPrice()
    {
        // Arrange
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: null);

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));
        await _sut.InitializeAsync();

        string? sharedText = null;
        _sut.ShareOverride = text =>
        {
            sharedText = text;
            return Task.CompletedTask;
        };

        // Act
        await _sut.ShareListingAsync();

        // Assert
        Assert.That(sharedText, Does.Contain("Private room with balcony near ICTU"));
        Assert.That(sharedText, Does.Contain("2,500,000 đ"));
    }

    [Test]
    public async Task GoBackAsync_WhenTapped_NavigatesBack()
    {
        // Arrange
        string? backRoute = null;
        _sut.NavigateOverride = route =>
        {
            backRoute = route;
            return Task.CompletedTask;
        };

        // Act
        await _sut.GoBackAsync();

        // Assert
        Assert.That(backRoute, Is.EqualTo(".."));
    }

    [Test]
    public async Task MessageLandlordAsync_WhenSignedIn_NavigatesToMessagesRoute()
    {
        // Arrange
        _sessionStore.HasValidTokenAsync().Returns(true);

        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: "0912345678");

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));
        await _sut.InitializeAsync();

        string? messageRoute = null;
        _sut.NavigateOverride = route =>
        {
            messageRoute = route;
            return Task.CompletedTask;
        };

        // Act
        await _sut.MessageLandlordAsync();

        // Assert
        Assert.That(messageRoute, Is.EqualTo("//messages?roomId=room-test-1&landlordId=landlord-1"));
    }

    [Test]
    public async Task MessageLandlordAsync_WhenGuest_PromptsSignIn()
    {
        // Arrange
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: null);

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));
        await _sut.InitializeAsync();

        bool alertPrompted = false;
        _sut.DisplayAlertOverride = (_, _, _, _) =>
        {
            alertPrompted = true;
            return Task.FromResult(false);
        };

        // Act
        await _sut.MessageLandlordAsync();

        // Assert
        Assert.That(alertPrompted, Is.True);
    }

    [Test]
    public async Task EmptyRoomId_SetsIsNotFoundImmediately()
    {
        // Arrange
        _sut.RoomId = "   ";

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.That(_sut.IsNotFound, Is.True);
        Assert.That(_sut.IsLoading, Is.False);
        Assert.That(_sut.IsContentVisible, Is.False);
    }

    [Test]
    public async Task Dispose_UnsubscribesFromLocalizedStrings_NoLongerUpdatesOnCultureChange()
    {
        // Arrange
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: null);

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 1.2,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));
        await _sut.InitializeAsync();
        Assert.That(_sut.StatusBadgeText, Is.EqualTo("Verified · Available"));

        // Act - Dispose the ViewModel
        _sut.Dispose();

        // Change culture to Vietnamese
        LocalizedStrings.Instance.SetCulture(new CultureInfo("vi-VN"));

        // Assert - Badge text must NOT update because handler was unsubscribed on Dispose
        Assert.That(_sut.StatusBadgeText, Is.EqualTo("Verified · Available"));
    }

    [Test]
    public void IsGuest_WhenSignedInChanges_UpdatesCorrectly()
    {
        // Assert initial guest state
        Assert.That(_sut.IsSignedIn, Is.False);
        Assert.That(_sut.IsGuest, Is.True);

        // Act & Assert when user is signed in
        _sut.IsSignedIn = true;
        Assert.That(_sut.IsGuest, Is.False);

        // Act & Assert when user signs out
        _sut.IsSignedIn = false;
        Assert.That(_sut.IsGuest, Is.True);
    }

    [Test]
    public async Task PromptSignInAsync_WhenAccepted_NavigatesToLogin()
    {
        // Arrange
        bool alertPrompted = false;
        string? navigatedRoute = null;

        _sut.DisplayAlertOverride = (title, message, accept, cancel) =>
        {
            alertPrompted = true;
            return Task.FromResult(true);
        };
        _sut.NavigateOverride = route =>
        {
            navigatedRoute = route;
            return Task.CompletedTask;
        };

        // Act
        await _sut.PromptSignInAsync();

        // Assert
        Assert.That(alertPrompted, Is.True);
        Assert.That(navigatedRoute, Is.EqualTo("//login"));
    }

    [Test]
    public async Task InitializeAsync_TravelTimesWithMetersAndKm_FormatsDistanceAndDurationPerMode()
    {
        var testRoom = CreateSampleRoom(
            ListingStatus.Available, true, 2500000, 2500000, 25.0, 2, Furnishing.Full, null, 0.5, null);
        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            CampusId: "ICTU",
            StraightLineDistanceKm: 0.5,
            IsFallback: false,
            Items:
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 850, 120, false),
                new TravelTimeItemDto(TravelMode.Bus, 850, 792, true),
                new TravelTimeItemDto(TravelMode.Walking, 1240, 900, false)
            ]));

        await _sut.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_sut.HasTravelTimes, Is.True);
            Assert.That(_sut.IsCommuteFallback, Is.False);
            Assert.That(_sut.CommuteTimes, Has.Count.EqualTo(3));
            Assert.That(_sut.CommuteTimes[0].DistanceFormatted, Is.EqualTo("850 m"));
            Assert.That(_sut.CommuteTimes[0].TimeFormatted, Is.EqualTo("2 mins"));
            Assert.That(_sut.CommuteTimes[1].DistanceFormatted, Is.EqualTo("850 m"));
            Assert.That(_sut.CommuteTimes[1].TimeFormatted, Is.EqualTo("~13 mins"));
            Assert.That(_sut.CommuteTimes[2].DistanceFormatted, Is.EqualTo("1.2 km"));
            Assert.That(_sut.CommuteTimes[2].TimeFormatted, Is.EqualTo("15 mins"));
        });
    }

    [Test]
    public async Task InitializeAsync_WhenCampusIdIsNullAndUserSignedIn_UsesProfileSelectedCampus()
    {
        _sut.CampusId = null;
        _sut.CampusName = "ICTU";
        _sessionStore.HasValidTokenAsync().Returns(true);
        _sessionStore.GetCachedUserProfileAsync().Returns(new UserProfileDto(
            "u-1", "student@ictu.edu.vn", "Student", "Student", "Local", null, "DTC01", true,
            DateTime.UtcNow, SelectedCampusId: "TNUS", PreferredLanguage: "en"));

        var testRoom = CreateSampleRoom(
            ListingStatus.Available, true, 2500000, 2500000, 25.0, 2, Furnishing.Full, null, 2.4, null);
        _roomGateway.GetRoomDetailAsync("room-test-1", "TNUS").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "TNUS").Returns(new TravelTimesDto(
            "TNUS", 2.4, false,
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 3100, 380, false),
                new TravelTimeItemDto(TravelMode.Bus, 3100, 1208, true),
                new TravelTimeItemDto(TravelMode.Walking, 2700, 1940, false)
            ]));

        await _sut.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_sut.CampusName, Is.EqualTo("TNUS"));
            Assert.That(_sut.CommuteHeaderFormatted, Does.Contain("TNUS"));
            Assert.That(_sut.HasTravelTimes, Is.True);
        });
        await _roomGateway.Received(1).GetRoomDetailAsync("room-test-1", "TNUS");
        await _roomGateway.Received(1).GetRoomTravelTimesAsync("room-test-1", "TNUS");
    }

    [Test]
    public async Task InitializeAsync_WhenCampusIdIsNullAndSearchStoreHasCampus_UsesSearchStoreCampus()
    {
        var searchStore = new RoomToRentRadar.Services.Search.Core.SearchCriteriaStore();
        searchStore.SetCampus("TNUE", "TNUE");
        using var sut = new RoomDetailPageModel(_roomGateway, _sessionStore, _favouriteGateway, searchStore)
        {
            RoomId = "room-test-1"
        };

        var testRoom = CreateSampleRoom(
            ListingStatus.Available, true, 2500000, 2500000, 25.0, 2, Furnishing.Full, null, 3.1, null);
        _roomGateway.GetRoomDetailAsync("room-test-1", "TNUE").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "TNUE").Returns(
            new TravelTimesDto("TNUE", 3.1, true, Array.Empty<TravelTimeItemDto>()));

        await sut.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(sut.CampusName, Is.EqualTo("TNUE"));
            Assert.That(sut.IsCommuteFallback, Is.True);
            Assert.That(sut.HasTravelTimes, Is.False);
        });
        await _roomGateway.Received(1).GetRoomTravelTimesAsync("room-test-1", "TNUE");
    }

    [Test]
    public async Task InitializeAsync_WhenTravelTimesFallback_HidesCommuteGridAndShowsStraightLineNotice()
    {
        var testRoom = CreateSampleRoom(
            ListingStatus.Available, true, 2500000, 2500000, 25.0, 2, Furnishing.Full, null, 1.2, null);
        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(
            new TravelTimesDto("ICTU", 1.2, IsFallback: true, Items: Array.Empty<TravelTimeItemDto>()));

        await _sut.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_sut.HasError, Is.False);
            Assert.That(_sut.IsCommuteFallback, Is.True);
            Assert.That(_sut.HasTravelTimes, Is.False);
            Assert.That(_sut.CommuteTimes, Is.Empty);
            Assert.That(_sut.StraightLineNotice, Does.Contain("1.2 km"));
            Assert.That(_sut.StraightLineNotice, Does.Contain("ICTU"));
        });
    }

    [Test]
    public async Task InitializeAsync_WhenTravelTimesGatewayThrows_HidesCommuteGridAndShowsStraightLineNotice()
    {
        var testRoom = CreateSampleRoom(
            ListingStatus.Available, true, 2500000, 2500000, 25.0, 2, Furnishing.Full, null, 1.4, null);
        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu")
            .Returns(Task.FromException<TravelTimesDto>(new HttpRequestException("Timeout")));

        await _sut.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_sut.HasError, Is.False);
            Assert.That(_sut.IsContentVisible, Is.True);
            Assert.That(_sut.IsCommuteFallback, Is.True);
            Assert.That(_sut.HasTravelTimes, Is.False);
            Assert.That(_sut.CommuteTimes, Is.Empty);
            Assert.That(_sut.StraightLineNotice, Does.Contain("1.4 km"));
        });
    }

    [Test]
    public async Task CultureChanged_UpdatesCommuteTimesAndStraightLineNoticeToVietnamese()
    {
        var testRoom = CreateSampleRoom(
            ListingStatus.Available, true, 2500000, 2500000, 25.0, 2, Furnishing.Full, null, 1.2, null);
        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(new TravelTimesDto(
            "ICTU", 1.2, false,
            [
                new TravelTimeItemDto(TravelMode.Motorbike, 1850, 480, false),
                new TravelTimeItemDto(TravelMode.Bus, 1850, 1140, true),
                new TravelTimeItemDto(TravelMode.Walking, 1500, 1500, false)
            ]));

        await _sut.InitializeAsync();

        LocalizedStrings.Instance.SetCulture(new CultureInfo("vi-VN"));

        Assert.Multiple(() =>
        {
            Assert.That(_sut.CommuteTimes[0].Label, Is.EqualTo("Xe máy"));
            Assert.That(_sut.CommuteTimes[0].TimeFormatted, Is.EqualTo("8 phút"));
            Assert.That(_sut.CommuteTimes[0].DistanceFormatted, Is.EqualTo("1,9 km"));
            Assert.That(_sut.CommuteTimes[1].Label, Is.EqualTo("Xe buýt (dự tính)"));
            Assert.That(_sut.CommuteTimes[1].TimeFormatted, Is.EqualTo("~19 phút"));
            Assert.That(_sut.CommuteTimes[2].Label, Is.EqualTo("Đi bộ"));
            Assert.That(_sut.CommuteHeaderFormatted, Does.Contain("Đến ICTU"));
            Assert.That(_sut.StraightLineNotice, Does.Contain("1,2 km"));
        });
    }
    private static RoomDetailDto CreateSampleRoom(
        ListingStatus status,
        bool isVerified,
        long monthlyRent,
        long deposit,
        double areaM2,
        int maxOccupants,
        Furnishing furnishing,
        DateOnly? availableFrom,
        double distanceKm,
        string? landlordPhone)
    {
        return new RoomDetailDto(
            Id: "room-test-1",
            Title: "Private room with balcony near ICTU",
            Description: "Beautiful spacious room with full natural light and private balcony.",
            RoomType: RoomType.BoardingRoom,
            Status: status,
            IsVerified: isVerified,
            MonthlyRentVnd: monthlyRent,
            DepositVnd: deposit,
            AreaM2: areaM2,
            MaxOccupants: maxOccupants,
            Furnishing: furnishing,
            AvailableFrom: availableFrom,
            Address: new AddressDto("Ngõ 12, đường Z115", "Quyết Thắng", "TP. Thái Nguyên"),
            Location: new GeoPointDto(21.585, 105.808),
            DistanceKm: distanceKm,
            Utilities: new UtilitiesView(
                ElectricityPriceVndPerKwh: 3500,
                WaterPriceVnd: 100000,
                WaterPriceUnit: WaterPriceUnit.PerPerson,
                InternetFeeVnd: 0,
                ParkingFeeVnd: 50000,
                OtherFeesNote: null,
                EstimatedMonthlyTotalVnd: 2950000),
            Amenities: new List<Amenity>
            {
                Amenity.AirConditioner,
                Amenity.WaterHeater,
                Amenity.PrivateBathroom,
                Amenity.Wifi,
                Amenity.Bed,
                Amenity.Fridge,
                Amenity.MotorbikeParking,
                Amenity.Wardrobe,
                Amenity.Desk,
                Amenity.Balcony,
                Amenity.WashingMachine
            },
            Safety: new SafetyDto(
                Score: 4,
                IsVerified: true,
                Features: new List<SafetyFeature>
                {
                    SafetyFeature.Cctv,
                    SafetyFeature.SecureEntrance,
                    SafetyFeature.FireSafety,
                    SafetyFeature.OwnerOrGuardOnSite
                }),
            HouseRules: new HouseRulesDto(
                QuietHoursStart: "22:00",
                QuietHoursEnd: "06:00",
                CurfewTime: "23:30",
                GuestPolicy: GuestPolicy.DaytimeOnly,
                GuestPolicyNote: null,
                CookingPolicy: CookingPolicy.SharedKitchenOnly,
                ParkingAvailable: true,
                ParkingRules: "Motorbikes in the yard, one per room",
                PetsAllowed: false,
                SmokingAllowed: false,
                OtherRules: null,
                VersionNumber: 1,
                UpdatedAtUtc: DateTime.UtcNow.AddDays(-3)),
            Photos: new List<PhotoDto>
            {
                new("photo-1", PhotoKind.Standard, "thumb1.jpg", "med1.jpg", "orig1.jpg", 800, 600, 0),
                new("photo-2", PhotoKind.Panorama, "thumb2.jpg", "med2.jpg", "orig2.jpg", 1920, 1080, 1)
            },
            Landlord: new LandlordContactDto(
                Id: "landlord-1",
                DisplayName: "Nguyễn Thị Hoa",
                IsVerified: true,
                MemberSinceUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                PhoneNumber: landlordPhone,
                ZaloUrl: landlordPhone != null ? $"https://zalo.me/{landlordPhone}" : null),
            IsFavourite: false,
            PublishedAtUtc: DateTime.UtcNow.AddDays(-10),
            LastUpdatedAtUtc: DateTime.UtcNow.AddDays(-3)
        );
    }
}
