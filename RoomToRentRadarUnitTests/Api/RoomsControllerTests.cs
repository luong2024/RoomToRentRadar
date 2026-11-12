using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using RoomToRentRadar.Api.Controllers;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;

namespace RoomToRentRadarUnitTests.Api;

[TestFixture]
public class RoomsControllerTests
{
    private IGetRoomDetailUseCase _getRoomDetailUseCase = null!;
    private IGetSimilarRoomsUseCase _getSimilarRoomsUseCase = null!;
    private RoomsController _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _getRoomDetailUseCase = Substitute.For<IGetRoomDetailUseCase>();
        _getSimilarRoomsUseCase = Substitute.For<IGetSimilarRoomsUseCase>();
        _sut = new RoomsController(_getRoomDetailUseCase, _getSimilarRoomsUseCase);
    }

    [Test]
    public void Constructor_WhenUseCaseIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RoomsController(null!));
    }

    [Test]
    public async Task GetRoomById_WhenRoomExists_Returns200OkWithRoomDetailDto()
    {
        // Arrange
        var sampleDto = CreateSampleDetailDto();

        _getRoomDetailUseCase.ExecuteAsync("room-001", Arg.Any<CancellationToken>())
            .Returns(sampleDto);

        // Act
        var actionResult = await _sut.GetRoomById("room-001", CancellationToken.None);

        // Assert
        Assert.That(actionResult, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)actionResult;
        Assert.That(okResult.Value, Is.EqualTo(sampleDto));
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task GetRoomById_WhenIdIsNullOrWhitespace_Returns400BadRequestWithProblemDetails(string invalidId)
    {
        // Act
        var actionResult = await _sut.GetRoomById(invalidId, CancellationToken.None);

        // Assert
        Assert.That(actionResult, Is.InstanceOf<BadRequestObjectResult>());
        var badRequestResult = (BadRequestObjectResult)actionResult;
        Assert.That(badRequestResult.Value, Is.InstanceOf<ProblemDetails>());

        var problemDetails = (ProblemDetails)badRequestResult.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(problemDetails.Status, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(problemDetails.Title, Is.EqualTo("Invalid room request"));
            Assert.That(problemDetails.Detail, Does.Contain("Room ID cannot be empty or whitespace."));
            Assert.That(problemDetails.Extensions["errorCode"]?.ToString(), Is.EqualTo("VALIDATION_FAILED"));
        });
    }

    [Test]
    public async Task GetRoomById_WhenRoomNotFound_Returns404NotFoundWithProblemDetails()
    {
        // Arrange
        _getRoomDetailUseCase.ExecuteAsync("non-existent-id", Arg.Any<CancellationToken>())
            .Returns((RoomDetailDto?)null);

        // Act
        var actionResult = await _sut.GetRoomById("non-existent-id", CancellationToken.None);

        // Assert
        Assert.That(actionResult, Is.InstanceOf<NotFoundObjectResult>());
        var notFoundResult = (NotFoundObjectResult)actionResult;
        Assert.That(notFoundResult.Value, Is.InstanceOf<ProblemDetails>());

        var problemDetails = (ProblemDetails)notFoundResult.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(problemDetails.Status, Is.EqualTo(StatusCodes.Status404NotFound));
            Assert.That(problemDetails.Title, Is.EqualTo("Room not found"));
            Assert.That(problemDetails.Detail, Does.Contain("Room listing with ID 'non-existent-id' was not found."));
            Assert.That(problemDetails.Extensions["errorCode"]?.ToString(), Is.EqualTo("ROOM_NOT_FOUND"));
        });
    }

    [Test]
    public async Task GetRoomById_PropagatesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        _getRoomDetailUseCase.ExecuteAsync("room-001", token).Returns((RoomDetailDto?)null);

        // Act
        await _sut.GetRoomById("room-001", token);

        // Assert
        await _getRoomDetailUseCase.Received(1).ExecuteAsync("room-001", token);
    }

    [Test]
    public async Task GetSimilarRooms_WhenRoomExists_Returns200OkWithSimilarRooms()
    {
        // Arrange
        var targetRoom = CreateSampleDetailDto("room-001");
        _getRoomDetailUseCase.ExecuteAsync("room-001", Arg.Any<CancellationToken>()).Returns(targetRoom);

        var similarCards = new List<RoomCardDto>
        {
            new("sim-1", "Phòng 1", 1500000m, 20.0, RoomType.BoardingRoom, 0.5, true, ListingStatus.Available, 21.58, 105.80)
        };
        _getSimilarRoomsUseCase.ExecuteAsync("room-001", null, Arg.Any<CancellationToken>()).Returns(similarCards);

        // Act
        var result = await _sut.GetSimilarRooms("room-001", CancellationToken.None);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result;
        Assert.That(okResult.Value, Is.EqualTo(similarCards));
    }

    [Test]
    public async Task GetSimilarRooms_WhenTargetRoomNotFound_Returns404NotFound()
    {
        // Arrange
        _getRoomDetailUseCase.ExecuteAsync("room-not-found", Arg.Any<CancellationToken>()).Returns((RoomDetailDto?)null);

        // Act
        var result = await _sut.GetSimilarRooms("room-not-found", CancellationToken.None);

        // Assert
        Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        var notFound = (NotFoundObjectResult)result;
        var problem = (ProblemDetails)notFound.Value!;
        Assert.That(problem.Extensions["errorCode"]?.ToString(), Is.EqualTo("ROOM_NOT_FOUND"));
    }

    private static RoomDetailDto CreateSampleDetailDto(
        string id = "room-001",
        string title = "Phòng trọ đẹp",
        ListingStatus status = ListingStatus.Available)
    {
        return new RoomDetailDto(
            Id: id,
            Title: title,
            Description: "Mô tả",
            RoomType: RoomType.BoardingRoom,
            Status: status,
            IsVerified: true,
            MonthlyRentVnd: 1500000m,
            DepositVnd: 1500000m,
            AreaM2: 20.0,
            Address: new AddressDto("Số 12 Đường Z115", "Quyết Thắng", "TP. Thái Nguyên"),
            Location: new GeoPointDto(21.5875, 105.8080),
            DistanceKm: 0.5,
            Utilities: new UtilitiesView(3500m, 25000m, "PerCubicMeter", 100000m, 0m, null, 1950000),
            Amenities: new[] { Amenity.AirConditioner, Amenity.Wifi },
            Safety: new SafetyDto(4, true, new[] { "Cctv", "SecureEntrance" }),
            Photos: new[] { new PhotoDto("p1", "Standard", "thumb.jpg", "medium.jpg", "orig.jpg", 0) },
            Landlord: new LandlordContactDto("landlord-1", "Chủ nhà", true, DateTime.UtcNow, "0912345678", "https://zalo.me/0912345678"),
            LastUpdatedAtUtc: DateTime.UtcNow
        );
    }
}
