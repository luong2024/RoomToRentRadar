using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    private GetRoomDetailUseCase _useCase = null!;
    private RoomsController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _roomRepository = new InMemoryRoomRepository();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _userContext = new HttpContextUserContextAdapter(_httpContextAccessor);
        _useCase = new GetRoomDetailUseCase(_roomRepository, _userContext);
        _controller = new RoomsController(_useCase);
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
        Assert.That(response.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)response.Result!;
        var room = (RoomDetailDto)okResult.Value!;

        Assert.That(room.Id, Is.EqualTo("room-ictu-001"));
        Assert.That(room.Title, Does.Contain("cổng chính ICTU"));
        Assert.That(room.MonthlyRent, Is.EqualTo(1800000m));
        Assert.That(room.Status, Is.EqualTo("Available"));
        Assert.That(room.IsAvailable, Is.True);
        Assert.That(room.LandlordPhoneNumber, Is.EqualTo("0988123456")); // AC4: Authenticated user sees phone
        Assert.That(room.UnavailableMessage, Is.Null);
        Assert.That(room.SimilarRooms, Is.Empty);
    }

    [Test]
    public async Task GetRoomById_AnonymousUser_ReturnsDetailsWithNullPhone()
    {
        // Arrange (AC4: Guest/unauthenticated user receives null phone number)
        SetUserAnonymous();

        // Act
        var response = await _controller.GetRoomById("room-ictu-001", CancellationToken.None);

        // Assert
        Assert.That(response.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)response.Result!;
        var room = (RoomDetailDto)okResult.Value!;

        Assert.That(room.Id, Is.EqualTo("room-ictu-001"));
        Assert.That(room.LandlordPhoneNumber, Is.Null); // AC4: Anonymous user receives null phone
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task GetRoomById_WhitespaceId_Returns400BadRequest(string invalidId)
    {
        // Act
        var response = await _controller.GetRoomById(invalidId, CancellationToken.None);

        // Assert
        Assert.That(response.Result, Is.InstanceOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)response.Result!;
        Assert.That(badRequest.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetRoomById_PendingVerificationRoom_Returns404NotFound()
    {
        // Arrange (M4: Unverified rooms must not be disclosed)
        var pendingRoom = new Room(
            id: "room-pending-01",
            title: "Phòng chờ duyệt",
            description: "Chưa duyệt",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            roomType: "Khép kín",
            status: ListingStatus.PendingReview
        );
        _roomRepository.AddOrUpdate(pendingRoom);

        // Act
        var response = await _controller.GetRoomById("room-pending-01", CancellationToken.None);

        // Assert
        Assert.That(response.Result, Is.InstanceOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task GetRoomById_UnavailableRoom_ReturnsMessageAndSuggestedSimilarRooms()
    {
        // Arrange (AC3: Unavailable room suggests similar available rooms)
        SetUserAuthenticated("student-002");

        // Act
        var response = await _controller.GetRoomById("room-ictu-003", CancellationToken.None);

        // Assert
        Assert.That(response.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)response.Result!;
        var room = (RoomDetailDto)okResult.Value!;

        Assert.That(room.Id, Is.EqualTo("room-ictu-003"));
        Assert.That(room.Status, Is.EqualTo("Unavailable"));
        Assert.That(room.IsAvailable, Is.False);
        Assert.That(room.UnavailableMessage, Is.Not.Null.And.Not.Empty);
        Assert.That(room.SimilarRooms, Has.Count.GreaterThan(0));

        // Similar rooms must all be available
        foreach (var similar in room.SimilarRooms)
        {
            Assert.That(similar.Id, Is.Not.EqualTo("room-ictu-003"));
            Assert.That(similar.Title, Is.Not.Empty);
            Assert.That(similar.MonthlyRent, Is.GreaterThan(0));
        }
    }

    [Test]
    public async Task GetRoomById_RentedRoom_ReturnsMessageAndSuggestedSimilarRooms()
    {
        // Arrange (AC3: Rented room suggests similar available rooms)
        SetUserAnonymous();

        // Act
        var response = await _controller.GetRoomById("room-ictu-004", CancellationToken.None);

        // Assert
        Assert.That(response.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)response.Result!;
        var room = (RoomDetailDto)okResult.Value!;

        Assert.That(room.Id, Is.EqualTo("room-ictu-004"));
        Assert.That(room.Status, Is.EqualTo("Rented"));
        Assert.That(room.IsAvailable, Is.False);
        Assert.That(room.UnavailableMessage, Is.Not.Null.And.Not.Empty);
        Assert.That(room.SimilarRooms, Has.Count.GreaterThan(0));
    }

    [Test]
    public async Task GetRoomById_NonExistentRoom_Returns404NotFound()
    {
        // Arrange
        SetUserAnonymous();

        // Act
        var response = await _controller.GetRoomById("room-not-found-xyz", CancellationToken.None);

        // Assert
        Assert.That(response.Result, Is.InstanceOf<NotFoundObjectResult>());
        var notFound = (NotFoundObjectResult)response.Result!;
        Assert.That(notFound.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }
}
