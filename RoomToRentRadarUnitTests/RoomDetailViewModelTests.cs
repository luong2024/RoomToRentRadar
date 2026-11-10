using Moq;
using NUnit.Framework;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.RoomDetails.Models;
using RoomToRentRadar.Features.RoomDetails.Services;
using RoomToRentRadar.Features.RoomDetails.ViewModels;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class RoomDetailViewModelTests
{
    private Mock<IRoomDetailService> _serviceMock = null!;
    private Mock<IErrorHandler> _errorHandlerMock = null!;
    private RoomDetailViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceMock = new Mock<IRoomDetailService>();
        _errorHandlerMock = new Mock<IErrorHandler>();
        _viewModel = new RoomDetailViewModel(_serviceMock.Object, _errorHandlerMock.Object);
    }

    [Test]
    public async Task AC1_LoadRoomDetails_ShouldPopulateAllFields()
    {
        // Arrange
        var fakeRoom = new RoomDetailModel
        {
            Id = "room-01",
            Title = "Phòng trọ đẹp",
            Price = 2000000,
            Deposit = 1000000,
            AreaSquareMeters = 25,
            Status = ListingStatus.Available,
            Amenities = [new RoomAmenityModel { Name = "Wifi" }],
            HouseRules = ["Không nuôi chó mèo"]
        };
        _serviceMock.Setup(s => s.GetRoomDetailAsync("room-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeRoom);

        _viewModel.RoomId = "room-01";

        // Act
        await _viewModel.LoadRoomDetailsAsync();

        // Assert
        Assert.That(_viewModel.Room, Is.Not.Null);
        Assert.That(_viewModel.Room!.Title, Is.EqualTo("Phòng trọ đẹp"));
        Assert.That(_viewModel.IsAvailable, Is.True);
        Assert.That(_viewModel.CanBookOrContact, Is.True);
    }

    [Test]
    public async Task AC2_ListingStatusAndLastUpdated_ShouldExposeCorrectProperties()
    {
        // Arrange
        var now = DateTime.Now;
        var fakeRoom = new RoomDetailModel
        {
            Id = "room-02",
            Status = ListingStatus.Rented,
            LastUpdated = now
        };
        _serviceMock.Setup(s => s.GetRoomDetailAsync("room-02", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeRoom);
        _serviceMock.Setup(s => s.GetSimilarRoomsAsync("room-02", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _viewModel.RoomId = "room-02";

        // Act
        await _viewModel.LoadRoomDetailsAsync();

        // Assert
        Assert.That(_viewModel.IsRented, Is.True);
        Assert.That(_viewModel.IsAvailable, Is.False);
        Assert.That(_viewModel.Room!.LastUpdated, Is.EqualTo(now));
    }

    [Test]
    public async Task AC3_UnavailableOrRentedRoom_ShouldDisableBooking_AndLoadSimilarRooms()
    {
        // Arrange
        var fakeRoom = new RoomDetailModel
        {
            Id = "room-03",
            Status = ListingStatus.Unavailable
        };
        var similarRooms = new List<SimilarRoomModel>
        {
            new() { Id = "sim-1", Title = "Phòng thay thế 1", Price = 1500000 }
        };

        _serviceMock.Setup(s => s.GetRoomDetailAsync("room-03", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeRoom);
        _serviceMock.Setup(s => s.GetSimilarRoomsAsync("room-03", It.IsAny<CancellationToken>()))
            .ReturnsAsync(similarRooms);

        _viewModel.RoomId = "room-03";

        // Act
        await _viewModel.LoadRoomDetailsAsync();

        // Assert
        Assert.That(_viewModel.IsUnavailable, Is.True);
        Assert.That(_viewModel.CanBookOrContact, Is.False, "Phòng unavailable không được cho phép đặt lịch!");
        Assert.That(_viewModel.ShowSimilarRooms, Is.True);
        Assert.That(_viewModel.SimilarRooms.Count, Is.EqualTo(1));
    }

    [Test]
    public void AC4_LandlordPhone_ShouldBeMasked_WhenUserNotLoggedIn()
    {
        // Arrange
        _viewModel.Room = new RoomDetailModel
        {
            LandlordPhone = "0987654321"
        };
        _viewModel.IsUserLoggedIn = false;

        // Assert
        Assert.That(_viewModel.CanViewPhoneNumber, Is.False);
        Assert.That(_viewModel.DisplayPhoneNumber, Is.EqualTo("Đăng nhập để xem số điện thoại"));

        // Act: Đăng nhập
        _viewModel.IsUserLoggedIn = true;

        // Assert
        Assert.That(_viewModel.CanViewPhoneNumber, Is.True);
        Assert.That(_viewModel.DisplayPhoneNumber, Is.EqualTo("0987654321"));
    }
}