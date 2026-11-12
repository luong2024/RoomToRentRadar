using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using RoomToRentRadar.Api.Controllers;
using RoomToRentRadar.Api.Security;
using RoomToRentRadar.Application.UseCases.Rooms;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Infrastructure.Adapters.Mocks;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadarUnitTests.Api;

[TestFixture]
public class RoomDetailEndpointIntegrationTests
{
    private InMemoryRoomRepository _roomRepository = null!;
    private IHttpContextAccessor _httpContextAccessor = null!;
    private HttpContextUserContextAdapter _userContext = null!;
    private GetRoomDetailUseCase _detailUseCase = null!;
    private GetSimilarRoomsUseCase _similarUseCase = null!;
    private RoomsController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _roomRepository = new InMemoryRoomRepository();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _userContext = new HttpContextUserContextAdapter(_httpContextAccessor);
        _detailUseCase = new GetRoomDetailUseCase(_roomRepository, _userContext);
        _similarUseCase = new GetSimilarRoomsUseCase(_roomRepository, _roomRepository);
        _controller = new RoomsController(_detailUseCase, _similarUseCase);
    }

    private void SetUserAuthenticated(string userId = "student-123")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("sub", userId),
            new Claim("role", "Student")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };
        _httpContextAccessor.HttpContext.Returns(context);
    }

    private void SetUserAnonymous()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        _httpContextAccessor.HttpContext.Returns(context);
    }

    [Test]
    public async Task GetRoomById_AuthenticatedUser_ReturnsFullDetailsWithPhone()
    {
        // Arrange (AC1, AC2, AC4)
        SetUserAuthenticated("student-001");

        // Act
        var response = await _controller.GetRoomById("room-ictu-001", CancellationToken.None);

        // Assert
        Assert.That(response, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)response;
        var room = (RoomDetailDto)okResult.Value!;

        Assert.That(room.Id, Is.EqualTo("room-ictu-001"));
        Assert.That(room.Title, Does.Contain("cổng chính ICTU"));
        Assert.That(room.MonthlyRent, Is.EqualTo(1800000m));
        Assert.That(room.Status, Is.EqualTo(ListingStatus.Available));
        Assert.That(room.IsAvailable, Is.True);
        Assert.That(room.LandlordPhoneNumber, Is.EqualTo("0988123456")); // AC4: Authenticated user sees phone
        Assert.That(room.Landlord.PhoneNumber, Is.EqualTo("0988123456"));
        Assert.That(room.Landlord.ZaloUrl, Does.Contain("0988123456"));
    }

    [Test]
    public async Task GetRoomById_AnonymousUser_ReturnsDetailsWithNullPhone()
    {
        // Arrange (AC4: Guest/unauthenticated user receives null phone number)
        SetUserAnonymous();

        // Act
        var response = await _controller.GetRoomById("room-ictu-001", CancellationToken.None);

        // Assert
        Assert.That(response, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)response;
        var room = (RoomDetailDto)okResult.Value!;

        Assert.That(room.Id, Is.EqualTo("room-ictu-001"));
        Assert.That(room.LandlordPhoneNumber, Is.Null); // AC4: Anonymous user receives null phone
        Assert.That(room.Landlord.PhoneNumber, Is.Null);
        Assert.That(room.Landlord.ZaloUrl, Is.Null);
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task GetRoomById_WhitespaceId_Returns400BadRequest(string invalidId)
    {
        // Act
        var response = await _controller.GetRoomById(invalidId, CancellationToken.None);

        // Assert
        Assert.That(response, Is.InstanceOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)response;
        var problem = (ProblemDetails)badRequest.Value!;
        Assert.That(problem.Extensions["errorCode"]?.ToString(), Is.EqualTo("VALIDATION_FAILED"));
    }

    [Test]
    public async Task GetRoomById_PendingVerificationRoom_Returns404NotFound()
    {
        // Arrange (BR-LST-04a: Unverified rooms must not be disclosed)
        var pendingRoom = new Room(
            id: "room-pending-01",
            title: "Phòng chờ duyệt",
            description: "Chưa duyệt",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            roomType: "BoardingRoom",
            status: ListingStatus.PendingReview
        );
        _roomRepository.AddOrUpdate(pendingRoom);

        // Act
        var response = await _controller.GetRoomById("room-pending-01", CancellationToken.None);

        // Assert
        Assert.That(response, Is.InstanceOf<NotFoundObjectResult>());
        var notFound = (NotFoundObjectResult)response;
        var problem = (ProblemDetails)notFound.Value!;
        Assert.That(problem.Extensions["errorCode"]?.ToString(), Is.EqualTo("ROOM_NOT_FOUND"));
    }

    [Test]
    public async Task GetRoomById_UnavailableRoom_ReturnsUnpublishedDetails()
    {
        // Arrange (BR-LST-04a: Unpublished room is viewable by public)
        SetUserAuthenticated("student-002");

        // Act
        var response = await _controller.GetRoomById("room-ictu-003", CancellationToken.None);

        // Assert
        Assert.That(response, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)response;
        var room = (RoomDetailDto)okResult.Value!;

        Assert.That(room.Id, Is.EqualTo("room-ictu-003"));
        Assert.That(room.Status, Is.EqualTo(ListingStatus.Unpublished));
        Assert.That(room.IsAvailable, Is.False);
    }

    [Test]
    public async Task GetRoomById_RentedRoom_ReturnsRentedDetails()
    {
        // Arrange (BR-LST-04a: Rented room is viewable by public)
        SetUserAnonymous();

        // Act
        var response = await _controller.GetRoomById("room-ictu-004", CancellationToken.None);

        // Assert
        Assert.That(response, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)response;
        var room = (RoomDetailDto)okResult.Value!;

        Assert.That(room.Id, Is.EqualTo("room-ictu-004"));
        Assert.That(room.Status, Is.EqualTo(ListingStatus.Rented));
        Assert.That(room.IsAvailable, Is.False);
    }

    [Test]
    public async Task GetRoomById_NonExistentRoom_Returns404NotFoundWithErrorCode()
    {
        // Arrange
        SetUserAnonymous();

        // Act
        var response = await _controller.GetRoomById("room-not-found-xyz", CancellationToken.None);

        // Assert
        Assert.That(response, Is.InstanceOf<NotFoundObjectResult>());
        var notFound = (NotFoundObjectResult)response;
        Assert.That(notFound.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        var problem = (ProblemDetails)notFound.Value!;
        Assert.That(problem.Extensions["errorCode"]?.ToString(), Is.EqualTo("ROOM_NOT_FOUND"));
    }

    [Test]
    public async Task GetSimilarRooms_ReturnsSimilarAvailableRoomsPer_BR_LST_10()
    {
        // Arrange
        SetUserAnonymous();

        // Act
        var response = await _controller.GetSimilarRooms("room-ictu-003", CancellationToken.None);

        // Assert
        Assert.That(response, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)response;
        var similar = (IReadOnlyList<RoomCardDto>)okResult.Value!;

        Assert.That(similar, Is.Not.Null);
        Assert.That(similar, Has.Count.LessThanOrEqualTo(5));

        foreach (var card in similar)
        {
            Assert.That(card.Id, Is.Not.EqualTo("room-ictu-003"), "Must exclude target room itself");
            Assert.That(card.IsAvailable, Is.True, "Must only return Available rooms");
        }
    }

    [Test]
    public async Task GetSimilarRooms_TargetRoomNotFound_Returns404NotFound()
    {
        var response = await _controller.GetSimilarRooms("room-non-existing", CancellationToken.None);

        Assert.That(response, Is.InstanceOf<NotFoundObjectResult>());
        var notFound = (NotFoundObjectResult)response;
        var problem = (ProblemDetails)notFound.Value!;
        Assert.That(problem.Extensions["errorCode"]?.ToString(), Is.EqualTo("ROOM_NOT_FOUND"));
    }

    [Test]
    public async Task GetRoomById_WithMatchingIfNoneMatch_Returns304NotModified()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["If-None-Match"] = "\"room-ictu-001\"";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var response = await _controller.GetRoomById("room-ictu-001", CancellationToken.None);

        // Assert
        Assert.That(response, Is.InstanceOf<StatusCodeResult>());
        var statusCodeResult = (StatusCodeResult)response;
        Assert.That(statusCodeResult.StatusCode, Is.EqualTo(StatusCodes.Status304NotModified));
    }
}
