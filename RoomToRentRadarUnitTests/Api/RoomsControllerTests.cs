using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Controllers;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;

namespace RoomToRentRadarUnitTests.Api;

[TestFixture]
public class RoomsControllerTests
{
    private IGetRoomDetailUseCase _getRoomDetailUseCase = null!;
    private RoomsController _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _getRoomDetailUseCase = Substitute.For<IGetRoomDetailUseCase>();
        _sut = new RoomsController(_getRoomDetailUseCase);
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
        var sampleDto = new RoomDetailDto(
            Id: "room-001",
            Title: "Phòng trọ đẹp",
            Description: "Mô tả",
            MonthlyRent: 1500000m,
            DepositAmount: 1500000m,
            AreaSquareMeters: 20.0,
            RoomType: "Khép kín",
            Amenities: new[] { "Wifi", "Điều hòa" },
            FullAddress: "Z115",
            LandlordName: "Chủ nhà",
            LandlordPhoneNumber: "0912345678",
            PhotoUrls: new[] { "photo.jpg" },
            Panorama360Url: null,
            HouseRules: new[] { "Nội quy" },
            Status: "Available",
            LastUpdatedAt: DateTime.UtcNow,
            IsAvailable: true,
            UnavailableMessage: null,
            SimilarRooms: Array.Empty<RoomCardDto>()
        );

        _getRoomDetailUseCase.ExecuteAsync("room-001", Arg.Any<CancellationToken>())
            .Returns(sampleDto);

        // Act
        var actionResult = await _sut.GetRoomById("room-001", CancellationToken.None);

        // Assert
        Assert.That(actionResult.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)actionResult.Result!;
        Assert.That(okResult.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        Assert.That(okResult.Value, Is.EqualTo(sampleDto));
    }

    [Test]
    public async Task GetRoomById_WhenRoomDoesNotExist_Returns404NotFound()
    {
        // Arrange
        _getRoomDetailUseCase.ExecuteAsync("non-existent", Arg.Any<CancellationToken>())
            .Returns((RoomDetailDto?)null);

        // Act
        var actionResult = await _sut.GetRoomById("non-existent", CancellationToken.None);

        // Assert
        Assert.That(actionResult.Result, Is.InstanceOf<NotFoundObjectResult>());
        var notFoundResult = (NotFoundObjectResult)actionResult.Result!;
        Assert.That(notFoundResult.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        Assert.That(notFoundResult.Value, Is.InstanceOf<ProblemDetails>());
        var problem = (ProblemDetails)notFoundResult.Value!;
        Assert.That(problem.Detail, Does.Contain("non-existent"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task GetRoomById_WhenRoomIdIsWhitespace_Returns404NotFoundWithoutCallingUseCase(string? invalidId)
    {
        // Act
        var actionResult = await _sut.GetRoomById(invalidId!, CancellationToken.None);

        // Assert
        Assert.That(actionResult.Result, Is.InstanceOf<NotFoundObjectResult>());
        await _getRoomDetailUseCase.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
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
}
