using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Authorization;
using RoomToRentRadar.Api.Controllers;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Responses.Landlord;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Inbound.UseCases.Landlord;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// UC-30..UC-33 / ADR-012: landlord CRUD forwards the authenticated user (anti-IDOR), surfaces
/// <c>ETag</c>, forwards the raw <c>If-Match</c> header and applies the documented RBAC policies.
/// </summary>
[TestFixture]
public class LandlordRoomsControllerTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 12, 8, 0, 0, DateTimeKind.Utc);

    private IListMyListingsUseCase _listMyListingsUseCase = null!;
    private ICreateListingUseCase _createListingUseCase = null!;
    private IGetMyListingUseCase _getMyListingUseCase = null!;
    private IUpdateListingUseCase _updateListingUseCase = null!;
    private IMarkListingRentedUseCase _markListingRentedUseCase = null!;
    private IMarkListingAvailableUseCase _markListingAvailableUseCase = null!;
    private IUnpublishListingUseCase _unpublishListingUseCase = null!;
    private IRepublishListingUseCase _republishListingUseCase = null!;
    private IResubmitListingUseCase _resubmitListingUseCase = null!;
    private LandlordRoomsController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _listMyListingsUseCase = Substitute.For<IListMyListingsUseCase>();
        _createListingUseCase = Substitute.For<ICreateListingUseCase>();
        _getMyListingUseCase = Substitute.For<IGetMyListingUseCase>();
        _updateListingUseCase = Substitute.For<IUpdateListingUseCase>();
        _markListingRentedUseCase = Substitute.For<IMarkListingRentedUseCase>();
        _markListingAvailableUseCase = Substitute.For<IMarkListingAvailableUseCase>();
        _unpublishListingUseCase = Substitute.For<IUnpublishListingUseCase>();
        _republishListingUseCase = Substitute.For<IRepublishListingUseCase>();
        _resubmitListingUseCase = Substitute.For<IResubmitListingUseCase>();

        _controller = new LandlordRoomsController(
            _listMyListingsUseCase,
            _createListingUseCase,
            _getMyListingUseCase,
            _updateListingUseCase,
            _markListingRentedUseCase,
            _markListingAvailableUseCase,
            _unpublishListingUseCase,
            _republishListingUseCase,
            _resubmitListingUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = VerifiedLandlordPrincipal() }
            }
        };
    }

    [TestCase(nameof(LandlordRoomsController.ListMyListings), AuthorizationPolicies.LandlordPolicy)]
    [TestCase(nameof(LandlordRoomsController.GetMyListing), AuthorizationPolicies.LandlordPolicy)]
    [TestCase(nameof(LandlordRoomsController.UpdateListing), AuthorizationPolicies.LandlordPolicy)]
    [TestCase(nameof(LandlordRoomsController.MarkListingRented), AuthorizationPolicies.LandlordPolicy)]
    [TestCase(nameof(LandlordRoomsController.MarkListingAvailable), AuthorizationPolicies.LandlordPolicy)]
    [TestCase(nameof(LandlordRoomsController.UnpublishListing), AuthorizationPolicies.LandlordPolicy)]
    [TestCase(nameof(LandlordRoomsController.CreateListing), AuthorizationPolicies.VerifiedLandlordPolicy)]
    [TestCase(nameof(LandlordRoomsController.RepublishListing), AuthorizationPolicies.VerifiedLandlordPolicy)]
    [TestCase(nameof(LandlordRoomsController.ResubmitListing), AuthorizationPolicies.VerifiedLandlordPolicy)]
    public void Actions_ApplyTheDocumentedAuthorizationPolicy(string actionName, string expectedPolicy)
    {
        var attribute = GetAction(actionName).GetCustomAttribute<AuthorizeAttribute>();

        Assert.That(attribute, Is.Not.Null, actionName);
        Assert.That(attribute!.Policy, Is.EqualTo(expectedPolicy), actionName);
    }

    [Test]
    public async Task ListMyListings_ScopesToTheCallerAndForwardsPaging()
    {
        var page = new PagedMyListingsDto(
            Array.Empty<MyListingSummaryDto>(),
            TotalCount: 0,
            PageNumber: 2,
            PageSize: 5,
            CountsByStatus: Enum.GetValues<ListingStatus>().ToDictionary(status => status.ToString(), _ => 0));
        ListMyListingsRequest? captured = null;

        _listMyListingsUseCase
            .ExecuteAsync(Arg.Do<ListMyListingsRequest>(request => captured = request), Arg.Any<CancellationToken>())
            .Returns(page);

        var result = await _controller.ListMyListings(ListingStatus.Available, 2, 5, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            Assert.That((result.Result as OkObjectResult)!.Value, Is.SameAs(page));
            Assert.That(captured, Is.Not.Null);
            Assert.That(captured!.LandlordId, Is.EqualTo("landlord-1"));
            Assert.That(captured.Status, Is.EqualTo(ListingStatus.Available));
            Assert.That(captured.PageNumber, Is.EqualTo(2));
            Assert.That(captured.PageSize, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task CreateListing_Returns201WithEtagAndLocation()
    {
        var listing = SampleListing();
        var detail = Detail(version: 1, id: "room-1", status: ListingStatus.PendingReview);
        CreateListingCommand? captured = null;

        _createListingUseCase
            .ExecuteAsync(Arg.Do<CreateListingCommand>(command => captured = command), Arg.Any<CancellationToken>())
            .Returns(detail);

        var result = await _controller.CreateListing(listing, CancellationToken.None);

        var objectResult = result.Result as ObjectResult;
        Assert.Multiple(() =>
        {
            Assert.That(objectResult, Is.Not.Null);
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status201Created));
            Assert.That(objectResult.Value, Is.SameAs(detail));
            Assert.That(_controller.Response.Headers.ETag.ToString(), Is.EqualTo("\"1\""));
            Assert.That(_controller.Response.Headers.Location.ToString(), Is.EqualTo("/api/landlord/rooms/room-1"));
            Assert.That(captured, Is.Not.Null);
            Assert.That(captured!.LandlordId, Is.EqualTo("landlord-1"));
            Assert.That(captured.Listing, Is.SameAs(listing));
        });
    }

    [Test]
    public async Task GetMyListing_Returns200WithEtag()
    {
        _getMyListingUseCase
            .ExecuteAsync("landlord-1", "room-1", Arg.Any<CancellationToken>())
            .Returns(Detail(version: 4));

        var result = await _controller.GetMyListing("room-1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            Assert.That(_controller.Response.Headers.ETag.ToString(), Is.EqualTo("\"4\""));
        });
    }

    [Test]
    public async Task UpdateListing_ForwardsRawIfMatchAndReturnsTheNewEtag()
    {
        _controller.Request.Headers["If-Match"] = "\"3\"";
        UpdateListingCommand? captured = null;

        _updateListingUseCase
            .ExecuteAsync(Arg.Do<UpdateListingCommand>(command => captured = command), Arg.Any<CancellationToken>())
            .Returns(Detail(version: 4));

        var result = await _controller.UpdateListing("room-1", SampleListing(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            Assert.That(_controller.Response.Headers.ETag.ToString(), Is.EqualTo("\"4\""));
            Assert.That(captured, Is.Not.Null);
            Assert.That(captured!.LandlordId, Is.EqualTo("landlord-1"));
            Assert.That(captured.RoomId, Is.EqualTo("room-1"));
            Assert.That(captured.IfMatch, Is.EqualTo("\"3\""));
        });
    }

    [Test]
    public async Task UpdateListing_WithoutIfMatchHeader_ForwardsNullSoTheUseCaseCanReturn428()
    {
        UpdateListingCommand? captured = null;

        _updateListingUseCase
            .ExecuteAsync(Arg.Do<UpdateListingCommand>(command => captured = command), Arg.Any<CancellationToken>())
            .Returns(Detail(version: 4));

        await _controller.UpdateListing("room-1", SampleListing(), CancellationToken.None);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.IfMatch, Is.Null);
    }

    [Test]
    public async Task MarkListingRented_ForwardsIfMatchAndReturnsTheNewEtag()
    {
        _controller.Request.Headers["If-Match"] = "\"2\"";
        TransitionMyListingCommand? captured = null;

        _markListingRentedUseCase
            .ExecuteAsync(Arg.Do<TransitionMyListingCommand>(command => captured = command), Arg.Any<CancellationToken>())
            .Returns(Detail(version: 3, status: ListingStatus.Rented));

        var result = await _controller.MarkListingRented("room-1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            Assert.That(_controller.Response.Headers.ETag.ToString(), Is.EqualTo("\"3\""));
            Assert.That(captured, Is.Not.Null);
            Assert.That(captured!.LandlordId, Is.EqualTo("landlord-1"));
            Assert.That(captured.RoomId, Is.EqualTo("room-1"));
            Assert.That(captured.IfMatch, Is.EqualTo("\"2\""));
        });
    }

    private static MethodInfo GetAction(string name) =>
        typeof(LandlordRoomsController).GetMethod(name)!;

    private static ClaimsPrincipal VerifiedLandlordPrincipal() =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "landlord-1"),
                new Claim(ClaimTypes.Role, nameof(UserRole.Landlord)),
                new Claim(AuthorizationPolicies.VerifiedClaimType, "true")
            ],
            authenticationType: "Test"));

    private static UpsertListingRequest SampleListing() => new(
        Title: "Phòng trọ Sinh viên Z115",
        Description: null,
        RoomType: RoomType.BoardingRoom,
        MonthlyRentVnd: 1_500_000L,
        DepositVnd: 1_500_000L,
        AreaM2: 18.0,
        MaxOccupants: 2,
        Furnishing: Furnishing.Basic,
        AvailableFrom: null,
        Address: new AddressDto("Z115", "Tân Thịnh"),
        Location: new GeoPointDto(21.5875, 105.8080),
        Utilities: null,
        Amenities: null,
        SafetyFeatures: null,
        PhotoIds: ["photo-1", "photo-2", "photo-3"],
        HouseRules: null);

    private static MyListingDetailDto Detail(
        int version,
        string id = "room-1",
        ListingStatus status = ListingStatus.PendingReview) => new(
        Id: id,
        Status: status,
        StatusReason: null,
        NeedsReReview: false,
        IsVerified: true,
        IsSafetyVerified: false,
        Version: version,
        Listing: SampleListing(),
        Photos: Array.Empty<PhotoDto>(),
        HouseRules: null,
        PublishedAtUtc: null,
        LastUpdatedAtUtc: FixedNow,
        CreatedAtUtc: FixedNow);
}
