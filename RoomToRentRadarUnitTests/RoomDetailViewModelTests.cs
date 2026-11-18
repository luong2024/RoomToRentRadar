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
    private Mock<IAuthenticationSessionService> _sessionServiceMock = null!;
    private RoomDetailViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceMock = new Mock<IRoomDetailService>();
        _errorHandlerMock = new Mock<IErrorHandler>();
        _sessionServiceMock = new Mock<IAuthenticationSessionService>();
        _viewModel = new RoomDetailViewModel(_serviceMock.Object, _errorHandlerMock.Object, _sessionServiceMock.Object);
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
    public async Task LoadRoomDetailsAsync_WhenNewLoadStarts_ClearsPreviousRoomBeforeAwaitingApi()
    {
        var pendingRoom = new TaskCompletionSource<RoomDetailModel?>();
        _viewModel.Room = new RoomDetailModel { Id = "previous", Status = ListingStatus.Available };
        _viewModel.RoomId = "next";
        _serviceMock.Setup(service => service.GetRoomDetailAsync("next", It.IsAny<CancellationToken>()))
            .Returns(pendingRoom.Task);

        var loadTask = _viewModel.LoadRoomDetailsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.Room, Is.Null);
            Assert.That(_viewModel.CanBookOrContact, Is.False);
        });

        pendingRoom.SetResult(new RoomDetailModel { Id = "next", Status = ListingStatus.Available });
        await loadTask;
    }

    [Test]
    public async Task LoadRoomDetailsAsync_WhenSupersededRequestFaults_DoesNotPropagateOrUpdateUi()
    {
        var staleRequest = new TaskCompletionSource<RoomDetailModel?>();
        _serviceMock.Setup(service => service.GetRoomDetailAsync("room-A", It.IsAny<CancellationToken>()))
            .Returns(staleRequest.Task);
        _serviceMock.Setup(service => service.GetRoomDetailAsync("room-B", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoomDetailModel { Id = "room-B", Status = ListingStatus.Available });

        _viewModel.RoomId = "room-A";
        var firstLoad = _viewModel.LoadRoomDetailsAsync();
        _viewModel.RoomId = "room-B";
        await _viewModel.LoadRoomDetailsAsync();
        staleRequest.SetException(new HttpRequestException("Late failure"));

        await firstLoad;

        Assert.That(_viewModel.Room!.Id, Is.EqualTo("room-B"));
        _errorHandlerMock.Verify(handler => handler.HandleError(It.IsAny<Exception>()), Times.Never);
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

        _viewModel.RoomId = "room-02";

        // Act
        await _viewModel.LoadRoomDetailsAsync();

        // Assert
        Assert.That(_viewModel.IsRented, Is.True);
        Assert.That(_viewModel.IsAvailable, Is.False);
        Assert.That(_viewModel.Room!.LastUpdated, Is.EqualTo(now));
    }

    [Test]
    public async Task AC3_UnavailableOrRentedRoom_ShouldDisableBooking_AndUseSimilarRoomsFromDetailResponse()
    {
        // Arrange
        var fakeRoom = new RoomDetailModel
        {
            Id = "room-03",
            Status = ListingStatus.Unavailable,
            SimilarRooms = []
        };
        var similarRooms = new List<SimilarRoomModel>
        {
            new() { Id = "sim-1", Title = "Phòng thay thế 1", Price = 1500000 }
        };
        fakeRoom.SimilarRooms = similarRooms;

        _serviceMock.Setup(s => s.GetRoomDetailAsync("room-03", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeRoom);

        _viewModel.RoomId = "room-03";

        // Act
        await _viewModel.LoadRoomDetailsAsync();

        // Assert
        Assert.That(_viewModel.IsUnavailable, Is.True);
        Assert.That(_viewModel.CanBookOrContact, Is.False, "Phòng unavailable không được cho phép đặt lịch!");
        Assert.That(_viewModel.ShowSimilarRooms, Is.True);
        Assert.That(_viewModel.SimilarRooms.Count, Is.EqualTo(1));
        _serviceMock.Verify(s => s.GetSimilarRoomsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void RoomInformation_ShouldRemainVisible_WhenUserIsNotLoggedIn()
    {
        // Arrange
        _viewModel.Room = new RoomDetailModel
        {
            LandlordPhone = "0987654321"
        };
        // Assert
        Assert.That(_viewModel.DisplayPhoneNumber, Is.EqualTo("0987654321"));
    }

    [Test]
    public void HasLandlordContact_WhenBackendReturnsMaskedPhone_IsFalse()
    {
        _viewModel.Room = new RoomDetailModel
        {
            LandlordPhone = "*** (Authentication required)"
        };

        Assert.That(_viewModel.HasLandlordContact, Is.False);
        Assert.That(_viewModel.CanCallLandlord, Is.False);
    }

    [Test]
    public async Task BookInspectionAsync_WhenUserIsNotAuthenticated_NavigatesToLogin()
    {
        string? navigatedRoute = null;
        _sessionServiceMock.SetupGet(session => session.IsAuthenticated).Returns(false);
        _viewModel = new RoomDetailViewModel(
            _serviceMock.Object,
            _errorHandlerMock.Object,
            _sessionServiceMock.Object,
            route =>
            {
                navigatedRoute = route;
                return Task.CompletedTask;
            })
        {
            Room = new RoomDetailModel { Status = ListingStatus.Available }
        };

        await _viewModel.BookInspectionAsync();

        Assert.That(navigatedRoute, Is.EqualTo("//auth/login"));
    }

    [Test]
    public async Task LoadRoomDetailsAsync_WhenRoomIsNotFound_ClearsPreviousSimilarRooms()
    {
        _viewModel.Room = new RoomDetailModel { Id = "previous-room" };
        _viewModel.SimilarRooms = [new SimilarRoomModel { Id = "previous-similar" }];
        _serviceMock.Setup(service => service.GetRoomDetailAsync("missing-room", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoomDetailModel?)null);
        _viewModel.RoomId = "missing-room";

        await _viewModel.LoadRoomDetailsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.Room, Is.Null);
            Assert.That(_viewModel.SimilarRooms, Is.Empty);
            Assert.That(_viewModel.IsNotFound, Is.True);
        });
    }

    [Test]
    public async Task LoadRoomDetailsAsync_WhenLoadingFails_ClearsPreviousSimilarRooms()
    {
        _viewModel.Room = new RoomDetailModel { Id = "previous-room" };
        _viewModel.SimilarRooms = [new SimilarRoomModel { Id = "previous-similar" }];
        _serviceMock.Setup(service => service.GetRoomDetailAsync("failed-room", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network failure"));
        _viewModel.RoomId = "failed-room";

        await _viewModel.LoadRoomDetailsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.Room, Is.Null);
            Assert.That(_viewModel.SimilarRooms, Is.Empty);
            Assert.That(_viewModel.IsNotFound, Is.True);
        });
        _errorHandlerMock.Verify(handler => handler.HandleError(It.IsAny<HttpRequestException>()), Times.Once);
    }

    [Test]
    public async Task ApplyQueryAttributes_WhenNavigatingToSameRoomAndNotBusy_RefreshesRoomDetails()
    {
        var initialRoom = new RoomDetailModel { Id = "room-01", Title = "Initial Title", Status = ListingStatus.Available };
        var updatedRoom = new RoomDetailModel { Id = "room-01", Title = "Updated Title", Status = ListingStatus.Available };

        _serviceMock.SetupSequence(service => service.GetRoomDetailAsync("room-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(initialRoom)
            .ReturnsAsync(updatedRoom);

        _viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = "room-01" });
        await _viewModel.LoadRoomDetailsCommand.ExecutionTask!;
        Assert.That(_viewModel.Room!.Title, Is.EqualTo("Initial Title"));

        // Reopen same room when not busy -> should refresh
        _viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = "room-01" });
        await _viewModel.LoadRoomDetailsCommand.ExecutionTask!;

        Assert.That(_viewModel.Room!.Title, Is.EqualTo("Updated Title"));
        _serviceMock.Verify(service => service.GetRoomDetailAsync("room-01", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public void ApplyQueryAttributes_WhenNavigatingToSameRoomAndIsBusy_DoesNotDuplicateRequest()
    {
        _viewModel.RoomId = "room-01";
        _viewModel.IsBusy = true;

        _viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = "room-01" });

        _serviceMock.Verify(service => service.GetRoomDetailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task LoadRoomDetailsAsync_WhenRoomIdIsWhitespace_ClearsPreviousRoomAndSimilarRooms()
    {
        _viewModel.Room = new RoomDetailModel { Id = "previous-room", Title = "Old Room" };
        _viewModel.SimilarRooms = [new SimilarRoomModel { Id = "previous-similar" }];
        _viewModel.RoomId = "   ";

        await _viewModel.LoadRoomDetailsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.IsBusy, Is.False);
            Assert.That(_viewModel.Room, Is.Null);
            Assert.That(_viewModel.SimilarRooms, Is.Empty);
            Assert.That(_viewModel.IsNotFound, Is.True);
            Assert.That(_viewModel.StatusMessage, Is.EqualTo("A room ID is required."));
            Assert.That(_viewModel.HasRoom, Is.False);
        });
    }

    [Test]
    public async Task ApplyQueryAttributes_WhenNavigatingToEmptyIdWhileLoading_CancelsRequestAndResetsIsBusy()
    {
        var roomATcs = new TaskCompletionSource<RoomDetailModel?>();

        _serviceMock.Setup(service => service.GetRoomDetailAsync("room-A", It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (id, token) =>
            {
                using var reg = token.Register(() => roomATcs.TrySetCanceled(token));
                return await roomATcs.Task;
            });

        // 1. Navigate to Room A (starts loading -> IsBusy = true)
        _viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = "room-A" });
        Assert.That(_viewModel.IsBusy, Is.True);

        // 2. Navigate to empty ID while Room A is loading
        _viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = string.Empty });

        // Wait for execution task
        await _viewModel.LoadRoomDetailsCommand.ExecutionTask!;

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.IsBusy, Is.False, "IsBusy must be reset to false when navigating to empty ID");
            Assert.That(_viewModel.Room, Is.Null);
            Assert.That(_viewModel.IsNotFound, Is.True);
            Assert.That(_viewModel.StatusMessage, Is.EqualTo("A room ID is required."));
        });
    }

    [Test]
    public async Task ApplyQueryAttributes_WhenSwitchingRoomsWhileFirstIsLoading_CancelsFirstAndLoadsSecond()
    {
        var roomATcs = new TaskCompletionSource<RoomDetailModel?>();
        var roomB = new RoomDetailModel { Id = "room-B", Title = "Room B Title", Status = ListingStatus.Available };

        _serviceMock.Setup(service => service.GetRoomDetailAsync("room-A", It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (id, token) =>
            {
                using var reg = token.Register(() => roomATcs.TrySetCanceled(token));
                return await roomATcs.Task;
            });

        _serviceMock.Setup(service => service.GetRoomDetailAsync("room-B", It.IsAny<CancellationToken>()))
            .ReturnsAsync(roomB);

        // 1. Navigate to Room A (starts loading)
        _viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = "room-A" });
        Assert.That(_viewModel.IsBusy, Is.True);
        Assert.That(_viewModel.RoomId, Is.EqualTo("room-A"));

        // 2. Navigate to Room B while Room A is still loading
        _viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = "room-B" });
        Assert.That(_viewModel.RoomId, Is.EqualTo("room-B"));

        // Wait for latest execution task
        await _viewModel.LoadRoomDetailsCommand.ExecutionTask!;

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.Room, Is.Not.Null);
            Assert.That(_viewModel.Room!.Id, Is.EqualTo("room-B"));
            Assert.That(_viewModel.Room.Title, Is.EqualTo("Room B Title"));
            Assert.That(_viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task BookInspectionAsync_WhenUserIsAuthenticated_DisplaysComingSoonAlert()
    {
        var alerts = new List<(string Title, string Message, string Accept, string? Cancel)>();
        _sessionServiceMock.SetupGet(session => session.IsAuthenticated).Returns(true);

        _viewModel = new RoomDetailViewModel(
            _serviceMock.Object,
            _errorHandlerMock.Object,
            _sessionServiceMock.Object,
            navigateAsync: null,
            displayAlertAsync: (title, message, accept, cancel) =>
            {
                alerts.Add((title, message, accept, cancel));
                return Task.FromResult(true);
            })
        {
            Room = new RoomDetailModel { Id = "room-01", Title = "Cozy Studio", Status = ListingStatus.Available }
        };

        await _viewModel.BookInspectionAsync();

        Assert.Multiple(() =>
        {
            Assert.That(alerts, Has.Count.EqualTo(1));
            Assert.That(alerts[0].Title, Is.EqualTo("Coming Soon"));
            Assert.That(alerts[0].Message, Is.EqualTo("Viewing appointment scheduling is not available yet."));
            Assert.That(alerts[0].Accept, Is.EqualTo("OK"));
            Assert.That(alerts[0].Cancel, Is.Null);
        });
    }
}
