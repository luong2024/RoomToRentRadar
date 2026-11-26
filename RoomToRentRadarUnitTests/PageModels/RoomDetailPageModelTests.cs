using System.Globalization;
using System.Net;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Pages.Controls.RoomDetail.Models;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadarUnitTests.PageModels;

[TestFixture]
public class RoomDetailPageModelTests
{
    private IRoomGateway _roomGateway = null!;
    private ISessionStore _sessionStore = null!;
    private INavigationService _navigation = null!;
    private IRoomDetailDeviceActions _deviceActions = null!;
    private IFavouriteGateway _favouriteGateway = null!;
    private RoomDetailPageModel _sut = null!;

    [SetUp]
    public void SetUp()
    {
        LocalizedStrings.Instance.SetCulture(new CultureInfo("en-US"));

        _roomGateway = Substitute.For<IRoomGateway>();
        _sessionStore = Substitute.For<ISessionStore>();
        _navigation = Substitute.For<INavigationService>();
        _deviceActions = Substitute.For<IRoomDetailDeviceActions>();
        _favouriteGateway = Substitute.For<IFavouriteGateway>();

        _sessionStore.HasValidTokenAsync().Returns(false);
        _sessionStore.GetCachedUserProfileAsync().Returns((UserProfileDto?)null);

        _sut = new RoomDetailPageModel(_roomGateway, _sessionStore, _navigation, _deviceActions, _favouriteGateway)
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
            availableFrom: new DateOnly(2099, 12, 1),
            distanceKm: 1.2,
            landlordPhone: null); // Hidden for guests (BR-CNT-01)

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(CreateSampleTravelTimes("ICTU", 1.2));

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
        Assert.That(_sut.PriceFormatted, Is.EqualTo("2.5M"));
        Assert.That(_sut.PeriodFormatted, Is.EqualTo("/mo"));
        Assert.That(_sut.DepositFormatted, Is.EqualTo("Deposit 2.5M VND (1 month)"));
        Assert.That(_sut.StatusBadgeText, Is.EqualTo("Verified · Available"));

        // Assert - Key Facts (6 cards grouped into 2 rows of 3)
        Assert.That(_sut.KeyFacts, Has.Count.EqualTo(6));
        Assert.That(_sut.KeyFactRows, Has.Count.EqualTo(2));
        Assert.That(_sut.KeyFactRows[0].First.Value, Is.EqualTo("25 m²"));
        Assert.That(_sut.KeyFactRows[0].Second!.Value, Is.EqualTo("Boarding room"));
        Assert.That(_sut.KeyFactRows[0].Third!.Value, Is.EqualTo("2 people"));
        Assert.That(_sut.KeyFactRows[1].First.Value, Is.EqualTo("Full"));
        Assert.That(_sut.KeyFactRows[1].Second!.Value, Is.EqualTo("1 Dec 2099"));
        Assert.That(_sut.KeyFactRows[1].Third!.Value, Is.EqualTo("1.2 km"));
        Assert.That(_sut.KeyFacts[0].Value, Is.EqualTo("25 m²"));
        Assert.That(_sut.KeyFacts[1].Value, Is.EqualTo("Boarding room"));
        Assert.That(_sut.KeyFacts[2].Value, Is.EqualTo("2 people"));
        Assert.That(_sut.KeyFacts[3].Value, Is.EqualTo("Full"));
        Assert.That(_sut.KeyFacts[4].Value, Is.EqualTo("1 Dec 2099"));
        Assert.That(_sut.KeyFacts[5].Value, Is.EqualTo("1.2 km"));

        // Assert - Monthly Costs (5 breakdown rows + estimated total)
        Assert.That(_sut.CostBreakdownRows, Has.Count.EqualTo(5));
        Assert.That(_sut.CostBreakdownRows[0].ValueFormatted, Is.EqualTo("2,500,000 ₫")); // Rent
        Assert.That(_sut.CostBreakdownRows[1].ValueFormatted, Is.EqualTo("3,500 ₫ / kWh")); // Electricity
        Assert.That(_sut.CostBreakdownRows[2].ValueFormatted, Is.EqualTo("100,000 ₫ / person")); // Water
        Assert.That(_sut.CostBreakdownRows[3].ValueFormatted, Is.EqualTo("Included")); // Internet
        Assert.That(_sut.CostBreakdownRows[4].ValueFormatted, Is.EqualTo("50,000 ₫")); // Parking
        Assert.That(_sut.EstimatedTotalFormatted, Is.EqualTo("~2.95M/mo"));

        // Assert - Amenities (11 provided, top 8 displayed initially)
        Assert.That(_sut.AmenitiesDisplayList, Has.Count.EqualTo(8));
        Assert.That(_sut.HasMoreAmenities, Is.True);
        Assert.That(_sut.SeeAllAmenitiesText, Is.EqualTo("See all 11 amenities"));

        // Assert - Safety
        Assert.That(_sut.SafetyScoreFormatted, Is.EqualTo("4/5 · Verified"));
        Assert.That(_sut.SafetyChecklist, Has.Count.EqualTo(5));

        // Assert - House Rules
        Assert.That(_sut.HouseRulesList, Has.Count.EqualTo(7));
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
        Assert.That(_sut.HouseRulesList[5].Title, Is.EqualTo("Pets"));
        Assert.That(_sut.HouseRulesList[5].Detail, Is.EqualTo("Not allowed"));
        Assert.That(_sut.HouseRulesList[6].Title, Is.EqualTo("Smoking"));
        Assert.That(_sut.HouseRulesList[6].Detail, Is.EqualTo("Not allowed"));

        // Assert - Commute Times
        Assert.That(_sut.CommuteTimes, Has.Count.EqualTo(3));
        Assert.That(_sut.HasTravelTimes, Is.True);
        Assert.That(_sut.IsCommuteFallback, Is.False);
        Assert.That(_sut.CommuteTimes[0].Mode, Is.EqualTo(TravelMode.Motorbike));
        Assert.That(_sut.CommuteTimes[0].TimeFormatted, Is.EqualTo("5 mins"));
        Assert.That(_sut.CommuteTimes[1].Mode, Is.EqualTo(TravelMode.Bus));
        Assert.That(_sut.CommuteTimes[1].TimeFormatted, Is.EqualTo("~18 mins"));
        Assert.That(_sut.CommuteTimes[2].Mode, Is.EqualTo(TravelMode.Walking));
        Assert.That(_sut.CommuteTimes[2].TimeFormatted, Is.EqualTo("20 mins"));

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
        _roomGateway.GetSimilarRoomsAsync("room-test-1", "ictu").Returns(similarRooms);

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.That(_sut.IsAvailable, Is.False);
        Assert.That(_sut.IsUnavailable, Is.True);
        Assert.That(_sut.UnavailableBannerText, Does.Contain("This room is no longer available. It was marked as rented"));
        Assert.That(_sut.UnavailableSummaryFormatted, Is.EqualTo("1.8M/mo · 18 m² · Boarding room"));
        Assert.That(_sut.ContactDisabledText, Is.EqualTo("Contact is disabled for unavailable rooms"));
        Assert.That(_sut.SimilarRooms, Has.Count.EqualTo(2));
        Assert.That(_sut.SimilarRooms[0].Title, Is.EqualTo("Room near Tân Thịnh market"));
        Assert.That(_sut.SimilarRooms[0].PriceFormatted, Is.EqualTo("1.9M/mo"));
        Assert.That(_sut.SimilarRooms[0].DistanceFormatted, Is.EqualTo("0.4 km from this room"));
    }

    [Test]
    public async Task InitializeAsync_WhenRoomIsUnpublished_SetsGeneralUnavailableBannerAndClearsOnAvailable()
    {
        // Arrange
        var unpublishedRoom = CreateSampleRoom(
            status: ListingStatus.Unpublished,
            isVerified: true,
            monthlyRent: 2800000,
            deposit: 2800000,
            areaM2: 28.0,
            maxOccupants: 3,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.0,
            landlordPhone: null);

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(unpublishedRoom);
        _roomGateway.GetSimilarRoomsAsync("room-test-1", "ictu").Returns(new List<RoomCardDto>
        {
            new("sim-1", "Similar", 2700000, 26.0, RoomType.BoardingRoom, 0.5, true, ListingStatus.Available, null, 21.5, 105.8, DateTime.UtcNow, false)
        });

        // Act
        await _sut.InitializeAsync();

        // Assert - Unpublished state
        Assert.That(_sut.IsUnavailable, Is.True);
        Assert.That(_sut.UnavailableBannerText, Is.EqualTo("This room is no longer available."));
        Assert.That(_sut.HasSimilarRooms, Is.True);

        // Act - Reload as Available room
        var availableRoom = CreateSampleRoom(
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
        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(availableRoom);
        await _sut.InitializeAsync();

        // Assert - Stale similar rooms are cleared
        Assert.That(_sut.IsAvailable, Is.True);
        Assert.That(_sut.IsUnavailable, Is.False);
        Assert.That(_sut.HasSimilarRooms, Is.False);
        Assert.That(_sut.SimilarRooms, Is.Empty);
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
        Assert.That(_sut.ErrorMessage, Is.EqualTo(AppResources.GetString("Error_Unexpected")));
        Assert.That(_sut.ErrorMessage, Does.Not.Contain("Network connection failed"));
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
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(CreateSampleTravelTimes("ICTU", 1.2));

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
        await _sut.InitializeAsync();

        Assert.That(_sut.AmenitiesDisplayList, Has.Count.EqualTo(8));
        Assert.That(_sut.AmenityRows, Has.Count.EqualTo(4));
        Assert.That(_sut.IsAllAmenitiesExpanded, Is.False);

        // Act 1 - Expand
        _sut.ToggleAllAmenities();

        // Assert 1
        Assert.That(_sut.IsAllAmenitiesExpanded, Is.True);
        Assert.That(_sut.AmenitiesDisplayList, Has.Count.EqualTo(11));
        Assert.That(_sut.AmenityRows, Has.Count.EqualTo(6));
        Assert.That(_sut.SeeAllAmenitiesText, Is.EqualTo("Collapse amenities"));

        // Act 2 - Collapse
        _sut.ToggleAllAmenities();

        // Assert 2
        Assert.That(_sut.IsAllAmenitiesExpanded, Is.False);
        Assert.That(_sut.AmenitiesDisplayList, Has.Count.EqualTo(8));
        Assert.That(_sut.AmenityRows, Has.Count.EqualTo(4));
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
        await _sut.InitializeAsync();

        _deviceActions
            .DisplayAlertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        // Act
        await _sut.CallLandlordAsync();

        // Assert
        await _deviceActions.Received(1).DisplayAlertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _navigation.Received(1).GoToAsync("//login");
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
        await _sut.InitializeAsync();

        // Act
        await _sut.CallLandlordAsync();

        // Assert
        await _deviceActions.Received(1).DialPhoneAsync("0912345678");
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
        await _sut.InitializeAsync();

        // Act
        await _sut.OpenZaloAsync();

        // Assert
        await _deviceActions.Received(1).OpenUriAsync("https://zalo.me/0912345678");
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
        // Act
        await _sut.SelectSimilarRoomAsync("sim-999");

        // Assert
        await _navigation.Received(1).GoToAsync($"{AppRoutes.RoomDetail}?roomId=sim-999&campusId=ictu");
    }

    [Test]
    public async Task SelectSimilarRoomAsync_WhenCampusIdIsNull_OmitsCampusQueryParameter()
    {
        _sut.CampusId = null;

        await _sut.SelectSimilarRoomAsync("sim-999");

        await _navigation.Received(1).GoToAsync($"{AppRoutes.RoomDetail}?roomId=sim-999");
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
        await _sut.InitializeAsync();

        // Act
        await _sut.ReportListingAsync();

        // Assert
        await _navigation.Received(1).GoToAsync("report?roomId=room-test-1");
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
        await _sut.InitializeAsync();

        Assert.That(_sut.StatusBadgeText, Is.EqualTo("Verified · Available"));

        // Act - Switch culture to Vietnamese
        LocalizedStrings.Instance.SetCulture(new CultureInfo("vi-VN"));

        // Assert
        Assert.That(_sut.StatusBadgeText, Is.EqualTo("Đã xác minh · Còn phòng"));
        Assert.That(_sut.PeriodFormatted, Is.EqualTo("/tháng"));
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
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(CreateSampleTravelTimes("ICTU", 1.2));

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.That(_sut.CostBreakdownRows[1].ValueFormatted, Is.EqualTo("Included")); // Electricity null -> Included
        Assert.That(_sut.CostBreakdownRows[2].ValueFormatted, Is.EqualTo("15,000 ₫ / m³")); // Water per m3
        Assert.That(_sut.CostBreakdownRows[3].ValueFormatted, Is.EqualTo("50,000 ₫")); // Internet
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
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(CreateSampleTravelTimes("ICTU", 1.2));

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.That(_sut.SafetyScoreFormatted, Is.EqualTo("2/5 · Unverified"));
        Assert.That(_sut.IsSafetyVerified, Is.False);
        Assert.That(_sut.SafetyChecklist, Has.Count.EqualTo(5));
        Assert.That(_sut.SafetyChecklist[0].IsVerified, Is.True); // CCTV
        Assert.That(_sut.SafetyChecklist[1].IsVerified, Is.False); // Secure entrance
        Assert.That(_sut.SafetyChecklist[2].IsVerified, Is.True); // Fire safety
    }

    [Test]
    public async Task PopulateSafety_WhenScoreIsZero_KeepsZeroAndDoesNotDefaultToFour()
    {
        // Arrange (BR-LST-08: zero safety score must never be inflated to 4/5)
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
            Safety = new SafetyDto(0, false, [])
        };

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(CreateSampleTravelTimes("ICTU", 1.2));

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.That(_sut.SafetyScoreFormatted, Is.EqualTo("0/5 · Unverified"));
        Assert.That(_sut.IsSafetyVerified, Is.False);
    }

    [Test]
    public async Task PopulateDescription_WhenAvailableAndNonEmpty_SetsShowDescriptionTrue()
    {
        // Arrange (US02 AC1)
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
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(CreateSampleTravelTimes("ICTU", 1.2));

        // Act - Available with description
        await _sut.InitializeAsync();
        Assert.That(_sut.Description, Is.EqualTo("Beautiful spacious room with full natural light and private balcony."));
        Assert.That(_sut.HasDescription, Is.True);
        Assert.That(_sut.ShowDescription, Is.True);

        // Act - Available with blank description
        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom with { Description = "   " });
        await _sut.InitializeAsync();
        Assert.That(_sut.HasDescription, Is.False);
        Assert.That(_sut.ShowDescription, Is.False);

        // Act - Unavailable hides description
        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom with { Status = ListingStatus.Rented });
        await _sut.InitializeAsync();
        Assert.That(_sut.ShowDescription, Is.False);
    }

    [Test]
    public async Task RefreshAsync_DoesNotShowLoadingSkeletonAndResetsIsBusy()
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
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(CreateSampleTravelTimes("ICTU", 1.2));
        await _sut.InitializeAsync();

        var seenIsLoadingTrue = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RoomDetailPageModel.IsLoading) && _sut.IsLoading)
            {
                seenIsLoadingTrue = true;
            }
        };

        // Act
        await _sut.RefreshAsync();

        // Assert
        Assert.That(seenIsLoadingTrue, Is.False);
        Assert.That(_sut.IsLoading, Is.False);
        Assert.That(_sut.IsBusy, Is.False);
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
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(CreateSampleTravelTimes("ICTU", 1.2));

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
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(CreateSampleTravelTimes("ICTU", 1.2));

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.That(_sut.HasPhotos, Is.True);
        Assert.That(_sut.Is360Available, Is.True);
        Assert.That(_sut.PhotoCounterText, Is.EqualTo("1 / 2"));
        Assert.That(_sut.CurrentPhotoIndexZeroBased, Is.EqualTo(0));
        Assert.That(_sut.HeroCoverUrl, Is.EqualTo("orig1.jpg"));

        // Act - Swipe to second photo in CarouselView
        _sut.CurrentPhotoIndexZeroBased = 1;

        // Assert - Counter updates to 2 / 2
        Assert.That(_sut.CurrentPhotoIndex, Is.EqualTo(2));
        Assert.That(_sut.PhotoCounterText, Is.EqualTo("2 / 2"));
    }

    [Test]
    public async Task Open360ViewAsync_WhenTapped_InvokesDeviceToast()
    {
        // Act
        await _sut.Open360ViewAsync();

        // Assert
        await _deviceActions.Received(1).DisplayToastAsync(AppResources.GetString("RoomDetail_360View"));
    }

    [Test]
    public async Task ShareListingAsync_WhenTapped_InvokesDeviceShareWithTitleAndPrice()
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
        await _sut.InitializeAsync();

        // Act
        await _sut.ShareListingAsync();

        // Assert
        await _deviceActions.Received(1).ShareTextAsync(
            "Private room with balcony near ICTU",
            Arg.Is<string>(text =>
                text.Contains("Private room with balcony near ICTU", StringComparison.Ordinal) &&
                text.Contains("2,500,000 ₫", StringComparison.Ordinal)));
    }

    [Test]
    public async Task GoBackAsync_WhenTapped_NavigatesBack()
    {
        // Act
        await _sut.GoBackAsync();

        // Assert
        await _navigation.Received(1).GoBackAsync();
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
        await _sut.InitializeAsync();

        // Act
        await _sut.MessageLandlordAsync();

        // Assert
        await _navigation.Received(1).GoToAsync("//messages?roomId=room-test-1&landlordId=landlord-1");
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
        await _sut.InitializeAsync();

        _deviceActions
            .DisplayAlertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        // Act
        await _sut.MessageLandlordAsync();

        // Assert
        await _deviceActions.Received(1).DisplayAlertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
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
    public async Task PreviewUnavailableDemoAsync_NavigatesToRentedSeedRoom()
    {
        await _sut.PreviewUnavailableDemoAsync();

        await _navigation.Received(1).GoToAsync($"{AppRoutes.RoomDetail}?roomId=seed-da-thue&campusId=ictu");
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
        _deviceActions
            .DisplayAlertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        // Act
        await _sut.PromptSignInAsync();

        // Assert
        await _deviceActions.Received(1).DisplayAlertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _navigation.Received(1).GoToAsync("//login");
    }

    [Test]
    public async Task InitializeAsync_WhenTravelTimesReturnsFallback_ShowsStraightLineNoticeAndHidesGrid()
    {
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

        _roomGateway.GetRoomDetailAsync("room-test-1", null).Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", null)
            .Returns(new TravelTimesDto("TNUS", 1.7, IsFallback: true, Items: []));

        _sut.CampusId = null;
        await _sut.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_sut.HasTravelTimes, Is.False);
            Assert.That(_sut.IsCommuteFallback, Is.True);
            Assert.That(_sut.CommuteTimes, Is.Empty);
            Assert.That(_sut.CampusName, Is.EqualTo("TNUS"));
            Assert.That(_sut.CommuteHeaderFormatted, Is.EqualTo("Getting to TNUS · 1.7 km"));
            Assert.That(_sut.StraightLineNotice, Is.EqualTo("Straight-line distance: 1.7 km to TNUS · travel times are unavailable right now"));
        });
    }

    [Test]
    public async Task InitializeAsync_WhenTravelTimesGatewayFails_DoesNotFabricateMinutesAndShowsFallbackNotice()
    {
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.4,
            landlordPhone: null);

        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu")
            .ThrowsAsync(new ApiException("Service unavailable", HttpStatusCode.ServiceUnavailable));

        await _sut.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_sut.HasTravelTimes, Is.False);
            Assert.That(_sut.IsCommuteFallback, Is.True);
            Assert.That(_sut.CommuteTimes, Is.Empty);
            Assert.That(_sut.StraightLineNotice, Is.EqualTo("Straight-line distance: 1.4 km to ictu · travel times are unavailable right now"));
        });
    }

    [Test]
    public async Task CultureChange_WithLoadedTravelTimes_RelocalizesModesWithoutRefetching()
    {
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
        _roomGateway.GetRoomTravelTimesAsync("room-test-1", "ictu").Returns(CreateSampleTravelTimes("ICTU", 1.2));

        await _sut.InitializeAsync();
        LocalizedStrings.Instance.SetCulture(new CultureInfo("vi-VN"));

        Assert.Multiple(() =>
        {
            Assert.That(_sut.HasTravelTimes, Is.True);
            Assert.That(_sut.CommuteTimes, Has.Count.EqualTo(3));
            Assert.That(_sut.CommuteTimes[0].Label, Is.EqualTo("Xe máy"));
            Assert.That(_sut.CommuteTimes[0].TimeFormatted, Is.EqualTo("5 phút"));
            Assert.That(_sut.CommuteTimes[1].Label, Is.EqualTo("Xe buýt (dự tính)"));
            Assert.That(_sut.CommuteTimes[1].TimeFormatted, Is.EqualTo("~18 phút"));
            Assert.That(_sut.CommuteTimes[2].Label, Is.EqualTo("Đi bộ"));
            Assert.That(_sut.CommuteTimes[2].TimeFormatted, Is.EqualTo("20 phút"));
        });
    }

    [Test]
    public async Task InitializeAsync_WhenOtherFeesNotePresent_ExposesFormattedOtherFeesNote()
    {
        var baseRoom = CreateSampleRoom(
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

        var testRoom = baseRoom with
        {
            Utilities = baseRoom.Utilities with { OtherFeesNote = "Rác 20.000 đ/tháng" }
        };
        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);

        await _sut.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_sut.CostBreakdownRows, Has.Count.EqualTo(5));
            Assert.That(_sut.HasOtherFeesNote, Is.True);
            Assert.That(_sut.OtherFeesNoteFormatted, Is.EqualTo("Other fees: Rác 20.000 đ/tháng"));
        });
    }

    [Test]
    public async Task PopulateHouseRules_WhenGuestNoteParkingUnavailableAndOtherRulesPresent_PopulatesAllFields()
    {
        var baseRoom = CreateSampleRoom(
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

        var testRoom = baseRoom with
        {
            HouseRules = new HouseRulesDto(
                QuietHoursStart: null,
                QuietHoursEnd: null,
                CurfewTime: null,
                GuestPolicy: GuestPolicy.DaytimeOnly,
                GuestPolicyNote: "Leave before 21:00",
                CookingPolicy: CookingPolicy.Allowed,
                ParkingAvailable: false,
                ParkingRules: null,
                PetsAllowed: true,
                SmokingAllowed: true,
                OtherRules: "Keep shared hallway clean",
                VersionNumber: 1,
                UpdatedAtUtc: DateTime.UtcNow)
        };
        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);

        await _sut.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_sut.HouseRulesList, Has.Count.EqualTo(6));
            Assert.That(_sut.HouseRulesList[0].Detail, Is.EqualTo("Daytime only · Leave before 21:00"));
            Assert.That(_sut.HouseRulesList[2].Detail, Is.EqualTo("No motorbike parking"));
            Assert.That(_sut.HouseRulesList[3].Title, Is.EqualTo("Pets"));
            Assert.That(_sut.HouseRulesList[3].Detail, Is.EqualTo("Allowed"));
            Assert.That(_sut.HouseRulesList[4].Title, Is.EqualTo("Smoking"));
            Assert.That(_sut.HouseRulesList[4].Detail, Is.EqualTo("Allowed"));
            Assert.That(_sut.HouseRulesList[5].Title, Is.EqualTo("Other rules"));
            Assert.That(_sut.HouseRulesList[5].Detail, Is.EqualTo("Keep shared hallway clean"));
        });
    }

    [Test]
    public async Task PopulateAmenities_WhenElevatorAndDryingAreaPresent_UsesLocalizedNames()
    {
        var baseRoom = CreateSampleRoom(
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

        var testRoom = baseRoom with
        {
            Amenities = [Amenity.Elevator, Amenity.DryingArea]
        };
        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);

        await _sut.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_sut.AmenitiesDisplayList[0].Name, Is.EqualTo("Elevator"));
            Assert.That(_sut.AmenitiesDisplayList[1].Name, Is.EqualTo("Drying area"));
        });

        LocalizedStrings.Instance.SetCulture(new CultureInfo("vi-VN"));
        await _sut.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_sut.AmenitiesDisplayList[0].Name, Is.EqualTo("Thang máy"));
            Assert.That(_sut.AmenitiesDisplayList[1].Name, Is.EqualTo("Khu phơi đồ"));
        });
    }

    [Test]
    public async Task InitializeAsync_WhenRoomIsUnpublished_SetsUnpublishedStatusBadge()
    {
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Unpublished,
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
        _roomGateway.GetSimilarRoomsAsync("room-test-1", "ictu").Returns([]);

        await _sut.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_sut.StatusBadgeText, Is.EqualTo(AppResources.GetString("RoomDetail_StatusUnpublished")));
            Assert.That(_sut.UpdatedAgoText, Is.EqualTo("Updated 3 days ago"));
            Assert.That(_sut.IsUnavailable, Is.True);
        });
    }

    [Test]
    public async Task InitializeAsync_WhenGatewayThrowsNetworkApiException_MapsLocalizedErrorMessage()
    {
        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu")
            .ThrowsAsync(new ApiException("SocketException: raw stack", 0, ApiErrorCodes.NetworkError));

        await _sut.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_sut.HasError, Is.True);
            Assert.That(_sut.ErrorMessage, Is.EqualTo(AppResources.GetString("Error_NETWORK_ERROR")));
            Assert.That(_sut.ErrorMessage, Does.Not.Contain("SocketException"));
        });
    }

    // T00.18: an amount the short form cannot show exactly is written in full (ui-ux-spec §6).
    [Test]
    public async Task InitializeAsync_WithAnOddRent_ShowsTheExactAmount()
    {
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2454321,
            deposit: 2454321,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: new DateOnly(2020, 1, 15),
            distanceKm: 1.2,
            landlordPhone: null);
        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);

        await _sut.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_sut.PriceFormatted, Is.EqualTo("2,454,321 ₫"));
            Assert.That(_sut.PeriodFormatted, Is.EqualTo("/mo"));
            Assert.That(_sut.PriceShortFormatted, Is.EqualTo("2,454,321 ₫/mo"));
            Assert.That(_sut.DepositFormatted, Is.EqualTo("Deposit 2,454,321 VND (1 month)"));
            Assert.That(_sut.DepositShortFormatted, Is.EqualTo("Deposit 2,454,321 VND"));
            Assert.That(_sut.CostBreakdownRows[0].ValueFormatted, Is.EqualTo("2,454,321 ₫"));
            Assert.That(_sut.KeyFacts[4].Value, Is.EqualTo("15 Jan 2020"));
        });
    }

    // T00.18: dates older than seven days are written as dates (ui-ux-spec §6).
    [TestCase("en-US", "d MMM yyyy", "Updated {0}", "marked as rented on {0}.")]
    [TestCase("vi-VN", "dd/MM/yyyy", "Cập nhật {0}", "đã thuê ngày {0}.")]
    public async Task InitializeAsync_WithAnOldListing_ShowsTheUpdateDate(
        string culture, string pattern, string updated, string banner)
    {
        var cultureInfo = new CultureInfo(culture);
        LocalizedStrings.Instance.SetCulture(cultureInfo);
        var lastUpdated = DateTime.UtcNow.AddDays(-24);
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Rented,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.2,
            landlordPhone: null) with { LastUpdatedAtUtc = lastUpdated };
        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetSimilarRoomsAsync("room-test-1", "ictu").Returns([]);

        await _sut.InitializeAsync();

        var date = lastUpdated.ToString(pattern, cultureInfo);
        Assert.Multiple(() =>
        {
            Assert.That(_sut.UpdatedAgoText, Is.EqualTo(string.Format(updated, date)));
            Assert.That(_sut.UnavailableBannerText, Does.EndWith(string.Format(banner, date)));
        });
    }

    [Test]
    public async Task InitializeAsync_WhenCultureIsVietnamese_FormatsShortPricesDistancesAndDatesPerLocale()
    {
        var viCulture = new CultureInfo("vi-VN");
        LocalizedStrings.Instance.SetCulture(viCulture);

        var availableDate = new DateOnly(2099, 12, 1);
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Rented,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 950000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: availableDate,
            distanceKm: 1.2,
            landlordPhone: null);
        _roomGateway.GetRoomDetailAsync("room-test-1", "ictu").Returns(testRoom);
        _roomGateway.GetSimilarRoomsAsync("room-test-1", "ictu").Returns(
        [
            new("sim-950k", "Phòng sinh viên", 950000, 18.0, RoomType.BoardingRoom, 0.4, true, ListingStatus.Available, null, 21.5, 105.8, DateTime.UtcNow, false)
        ]);

        await _sut.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_sut.PriceShortFormatted, Is.EqualTo("2,5 tr/tháng"));
            Assert.That(_sut.DepositShortFormatted, Is.EqualTo("Cọc 950k"));
            Assert.That(_sut.EstimatedTotalFormatted, Is.EqualTo("~2,95 tr/tháng"));
            Assert.That(_sut.KeyFacts[4].Value, Is.EqualTo("01/12/2099"));
            Assert.That(_sut.KeyFacts[5].Value, Is.EqualTo("1,2 km"));
            Assert.That(_sut.SimilarRooms[0].PriceFormatted, Is.EqualTo("950k/tháng"));
            Assert.That(_sut.SimilarRooms[0].DistanceFormatted, Is.EqualTo("Cách phòng này 0,4 km"));
            Assert.That(_sut.LandlordBadge, Does.Contain(testRoom.Landlord.MemberSinceUtc.ToString("MMM yyyy", viCulture)));
            Assert.That(_sut.StraightLineNotice, Does.Contain("1,2 km"));
        });
    }

    [Test]
    public async Task InitializeAsync_WithNonDefaultCampusId_UpdatesKeyFactsCampusLabelOnFirstLoad()
    {
        var testRoom = CreateSampleRoom(
            status: ListingStatus.Available,
            isVerified: true,
            monthlyRent: 2500000,
            deposit: 2500000,
            areaM2: 25.0,
            maxOccupants: 2,
            furnishing: Furnishing.Full,
            availableFrom: null,
            distanceKm: 1.8,
            landlordPhone: null);
        _roomGateway.GetRoomDetailAsync("room-test-1", "TNUT").Returns(testRoom);

        using var sut = new RoomDetailPageModel(_roomGateway, _sessionStore, _navigation, _deviceActions, _favouriteGateway)
        {
            RoomId = "room-test-1",
            CampusId = "TNUT"
        };

        await sut.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(sut.CampusName, Is.EqualTo("TNUT"));
            Assert.That(sut.KeyFacts[5].Label, Is.EqualTo("To TNUT"));
            Assert.That(sut.KeyFacts[5].Value, Is.EqualTo("1.8 km"));
            Assert.That(sut.CommuteHeaderFormatted, Does.StartWith("Getting to TNUT"));
        });
    }

    private static TravelTimesDto CreateSampleTravelTimes(string campusId, double straightLineKm) => new(
        CampusId: campusId,
        StraightLineDistanceKm: straightLineKm,
        IsFallback: false,
        Items:
        [
            new(TravelMode.Motorbike, 1850, 300, false),
            new(TravelMode.Bus, 1850, 1080, true),
            new(TravelMode.Walking, 1620, 1180, false)
        ]);

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
