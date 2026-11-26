using Moq;
using NUnit.Framework;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Models.RoomDetails;
using RoomToRentRadar.Services.RoomDetails;
using RoomToRentRadar.Services.RoomListings;
using RoomToRentRadar.ViewModels.RoomDetails;

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
    public async Task SelectDestinationAsync_UsesRoomAsOriginAndDisplaysSelectedCoordinates()
    {
        var travel = new Mock<ITravelTimeService>();
        travel.Setup(s => s.GetAsync("room-01", 21.5, 105.8, 21.6, 105.9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelInfo(3.4, 10, false));
        _viewModel = new RoomDetailViewModel(_serviceMock.Object, _errorHandlerMock.Object, _sessionServiceMock.Object, travelTimeService: travel.Object)
        {
            Room = new RoomDetailModel { Id = "room-01", Latitude = 21.5, Longitude = 105.8 }
        };

        await _viewModel.SelectDestinationAsync(21.6, 105.9);

        Assert.That(_viewModel.DestinationCoordinates, Is.EqualTo("Destination: 21.600000, 105.900000"));
        Assert.That(_viewModel.DistanceText, Is.EqualTo("3.4 km"));
        Assert.That(_viewModel.TravelTimeText, Is.EqualTo("~10 min driving"));
        travel.VerifyAll();
    }

    [Test]
    public async Task SelectDestinationAsync_OlderRequestCompletesLast_KeepsNewestResult()
    {
        var first = new TaskCompletionSource<TravelInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var travel = new Mock<ITravelTimeService>();
        travel.Setup(s => s.GetAsync("room-01", 21.5, 105.8, 21.6, 105.9, It.IsAny<CancellationToken>())).Returns(first.Task);
        travel.Setup(s => s.GetAsync("room-01", 21.5, 105.8, 21.7, 105.9, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelInfo(5, null, true));
        _viewModel = new RoomDetailViewModel(_serviceMock.Object, _errorHandlerMock.Object, _sessionServiceMock.Object, travelTimeService: travel.Object)
        {
            Room = new RoomDetailModel { Id = "room-01", Latitude = 21.5, Longitude = 105.8 }
        };

        var oldRequest = _viewModel.SelectDestinationAsync(21.6, 105.9);
        await _viewModel.SelectDestinationAsync(21.7, 105.9);
        first.SetResult(new TravelInfo(3.4, 10, false));
        await oldRequest;

        Assert.That(_viewModel.DestinationCoordinates, Is.EqualTo("Destination: 21.700000, 105.900000"));
        Assert.That(_viewModel.DistanceText, Is.EqualTo("5.0 km"));
        Assert.That(_viewModel.TravelTimeText, Is.EqualTo("Travel time unavailable"));
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
    public async Task LoadRoomDetailsAsync_WhenUserHasFavourite_RestoresFavouriteButtonState()
    {
        _sessionServiceMock.SetupGet(session => session.IsAuthenticated).Returns(true);
        var favourites = new Mock<IFavouritesService>();
        favourites.Setup(service => service.GetMyFavouriteIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>(["room-01"], StringComparer.Ordinal));
        _viewModel = new RoomDetailViewModel(_serviceMock.Object, _errorHandlerMock.Object, _sessionServiceMock.Object, favouritesService: favourites.Object);
        _serviceMock.Setup(service => service.GetRoomDetailAsync("room-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoomDetailModel { Id = "room-01", Status = ListingStatus.Available });
        _viewModel.RoomId = "room-01";

        await _viewModel.LoadRoomDetailsAsync();

        Assert.That(_viewModel.IsFavorite, Is.True);
    }

    [Test]
    public async Task ToggleFavoriteAsync_WhenAddingFavourite_PersistsAndUpdatesButtonState()
    {
        _sessionServiceMock.SetupGet(session => session.IsAuthenticated).Returns(true);
        var favourites = new Mock<IFavouritesService>();
        favourites.Setup(service => service.GetMyFavouriteIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        _viewModel = new RoomDetailViewModel(_serviceMock.Object, _errorHandlerMock.Object, _sessionServiceMock.Object, favouritesService: favourites.Object)
        {
            RoomId = SetupFavoriteRoom()
        };

        await _viewModel.LoadRoomDetailsAsync();
        await _viewModel.ToggleFavoriteCommand.ExecuteAsync(null);

        Assert.That(_viewModel.IsFavorite, Is.True);
        favourites.Verify(service => service.AddFavouriteAsync("room-01", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task Undo_AfterRemoval_RestoresOnlyInOriginalSession(bool signedInAgain, bool sameUser)
    {
        var originalUser = new AuthUser(Guid.NewGuid(), "student@example.com", UserRoleConstants.Student, "LOCAL", true);
        _sessionServiceMock.SetupGet(session => session.CurrentUser).Returns(originalUser);
        _sessionServiceMock.SetupGet(session => session.IsAuthenticated).Returns(true);
        _sessionServiceMock.SetupGet(session => session.SessionEpoch).Returns(1);
        var favourites = new Mock<IFavouritesService>();
        favourites.Setup(service => service.GetMyFavouriteIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "room-01" });
        var feedback = new Mock<IFavoriteFeedbackService>();
        Func<Task>? undo = null;
        feedback.Setup(service => service.ShowUndoAsync(It.IsAny<string>(), "Undo", It.IsAny<Func<Task>>()))
            .Callback<string, string, Func<Task>>((_, _, action) => undo = action)
            .Returns(Task.CompletedTask);
        _viewModel = new RoomDetailViewModel(
            _serviceMock.Object, _errorHandlerMock.Object, _sessionServiceMock.Object,
            favouritesService: favourites.Object, favoriteFeedbackService: feedback.Object)
        {
            RoomId = SetupFavoriteRoom()
        };

        await _viewModel.LoadRoomDetailsAsync();
        await _viewModel.ToggleFavoriteCommand.ExecuteAsync(null);
        favourites.Verify(service => service.RemoveFavouriteAsync("room-01", It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(undo, Is.Not.Null);

        if (signedInAgain)
        {
            _sessionServiceMock.SetupGet(session => session.CurrentUser).Returns((AuthUser?)null);
            _sessionServiceMock.SetupGet(session => session.IsAuthenticated).Returns(false);
            _sessionServiceMock.SetupGet(session => session.SessionEpoch).Returns(2);
            var signedInUser = sameUser ? originalUser : originalUser with { Id = Guid.NewGuid() };
            _sessionServiceMock.SetupGet(session => session.CurrentUser).Returns(signedInUser);
            _sessionServiceMock.SetupGet(session => session.IsAuthenticated).Returns(true);
            _sessionServiceMock.SetupGet(session => session.SessionEpoch).Returns(3);
        }

        await undo!();

        favourites.Verify(service => service.AddFavouriteAsync("room-01", It.IsAny<CancellationToken>()),
            signedInAgain ? Times.Never() : Times.Once());
        Assert.That(_viewModel.IsFavorite, Is.EqualTo(!signedInAgain));
    }

    [Test]
    public async Task ToggleFavoriteAsync_WhenUnauthenticated_ShowsInlineFavoriteError()
    {
        var favourites = new Mock<IFavouritesService>();
        _viewModel = new RoomDetailViewModel(_serviceMock.Object, _errorHandlerMock.Object, _sessionServiceMock.Object, favouritesService: favourites.Object)
        {
            Room = new RoomDetailModel { Id = "room-01", Status = ListingStatus.Available }
        };

        await _viewModel.ToggleFavoriteCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.FavoriteErrorMessage, Is.EqualTo("Sign in to save favorite rooms."));
            Assert.That(_viewModel.HasFavoriteError, Is.True);
        });
    }

    [Test]
    public async Task ToggleFavoriteAsync_WhenApiFails_ShowsInlineFavoriteError()
    {
        _sessionServiceMock.SetupGet(session => session.IsAuthenticated).Returns(true);
        var favourites = new Mock<IFavouritesService>();
        favourites.Setup(service => service.GetMyFavouriteIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        favourites.Setup(service => service.AddFavouriteAsync("room-01", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"));
        _viewModel = new RoomDetailViewModel(_serviceMock.Object, _errorHandlerMock.Object, _sessionServiceMock.Object, favouritesService: favourites.Object)
        {
            RoomId = SetupFavoriteRoom()
        };

        await _viewModel.LoadRoomDetailsAsync();
        await _viewModel.ToggleFavoriteCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.FavoriteErrorMessage, Is.EqualTo("Unable to update favorites. Please try again."));
            Assert.That(_viewModel.HasFavoriteError, Is.True);
        });
    }

    [Test]
    public async Task ToggleFavoriteAsync_WhileStatusIsUnknown_WaitsForServerStatusBeforeAllowingRemove()
    {
        _sessionServiceMock.SetupGet(session => session.IsAuthenticated).Returns(true);
        var pendingStatus = new TaskCompletionSource<IReadOnlySet<string>>();
        var favourites = new Mock<IFavouritesService>();
        favourites.Setup(service => service.GetMyFavouriteIdsAsync(It.IsAny<CancellationToken>())).Returns(pendingStatus.Task);
        _viewModel = new RoomDetailViewModel(_serviceMock.Object, _errorHandlerMock.Object, _sessionServiceMock.Object, favouritesService: favourites.Object)
        {
            RoomId = SetupFavoriteRoom()
        };

        var load = _viewModel.LoadRoomDetailsAsync();
        Assert.That(_viewModel.Room, Is.Not.Null);
        Assert.That(_viewModel.CanToggleFavorite, Is.False);
        await _viewModel.ToggleFavoriteCommand.ExecuteAsync(null);
        favourites.Verify(service => service.AddFavouriteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        favourites.Verify(service => service.RemoveFavouriteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        pendingStatus.SetResult(new HashSet<string> { "room-01" });
        await load;
        Assert.That(_viewModel.CanToggleFavorite, Is.True);
        await _viewModel.ToggleFavoriteCommand.ExecuteAsync(null);

        favourites.Verify(service => service.RemoveFavouriteAsync("room-01", It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(_viewModel.IsFavorite, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ToggleFavoriteAsync_WhenStatusRequestFailsOrSessionChanges_BlocksMutation(bool sessionChanged)
    {
        _sessionServiceMock.SetupGet(session => session.IsAuthenticated).Returns(true);
        _sessionServiceMock.SetupGet(session => session.SessionEpoch).Returns(1);
        var pendingStatus = new TaskCompletionSource<IReadOnlySet<string>>();
        var favourites = new Mock<IFavouritesService>();
        favourites.Setup(service => service.GetMyFavouriteIdsAsync(It.IsAny<CancellationToken>())).Returns(pendingStatus.Task);
        _viewModel = new RoomDetailViewModel(_serviceMock.Object, _errorHandlerMock.Object, _sessionServiceMock.Object, favouritesService: favourites.Object)
        {
            RoomId = SetupFavoriteRoom()
        };

        var load = _viewModel.LoadRoomDetailsAsync();
        if (sessionChanged)
        {
            _sessionServiceMock.SetupGet(session => session.SessionEpoch).Returns(2);
            pendingStatus.SetResult(new HashSet<string> { "room-01" });
        }
        else
        {
            pendingStatus.SetException(new HttpRequestException("offline"));
        }

        await load;
        await _viewModel.ToggleFavoriteCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.CanToggleFavorite, Is.False);
            Assert.That(_viewModel.IsFavorite, Is.False);
            Assert.That(_viewModel.HasFavoriteError, Is.True);
        });
        favourites.Verify(service => service.AddFavouriteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        favourites.Verify(service => service.RemoveFavouriteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task LoadRoomDetailsAsync_WhenUndoCompletesDuringStatusLoad_KeepsRestoredFavorite()
    {
        var user = new AuthUser(Guid.NewGuid(), "student@example.com", UserRoleConstants.Student, "LOCAL", true);
        _sessionServiceMock.SetupGet(session => session.CurrentUser).Returns(user);
        _sessionServiceMock.SetupGet(session => session.IsAuthenticated).Returns(true);
        var pendingStatus = new TaskCompletionSource<IReadOnlySet<string>>();
        var favourites = new Mock<IFavouritesService>();
        favourites.SetupSequence(service => service.GetMyFavouriteIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "room-01" })
            .Returns(pendingStatus.Task);
        var feedback = new Mock<IFavoriteFeedbackService>();
        Func<Task>? undo = null;
        feedback.Setup(service => service.ShowUndoAsync(It.IsAny<string>(), "Undo", It.IsAny<Func<Task>>()))
            .Callback<string, string, Func<Task>>((_, _, action) => undo = action)
            .Returns(Task.CompletedTask);
        _viewModel = new RoomDetailViewModel(
            _serviceMock.Object, _errorHandlerMock.Object, _sessionServiceMock.Object,
            favouritesService: favourites.Object, favoriteFeedbackService: feedback.Object)
        {
            RoomId = SetupFavoriteRoom()
        };

        await _viewModel.LoadRoomDetailsAsync();
        await _viewModel.ToggleFavoriteCommand.ExecuteAsync(null);
        Assert.That(undo, Is.Not.Null);

        var load = _viewModel.LoadRoomDetailsAsync();
        Assert.That(load.IsCompleted, Is.False);
        await undo!();
        pendingStatus.SetResult(new HashSet<string>());
        await load;

        Assert.That(_viewModel.IsFavorite, Is.True);
        favourites.Verify(service => service.AddFavouriteAsync("room-01", It.IsAny<CancellationToken>()), Times.Once);
    }

    private string SetupFavoriteRoom()
    {
        const string roomId = "room-01";
        _serviceMock.Setup(service => service.GetRoomDetailAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoomDetailModel { Id = roomId, Status = ListingStatus.Available });
        return roomId;
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

    [TestCase(false)]
    [TestCase(true)]
    public async Task ApplyQueryAttributes_WhenFavoriteStatusLoadIsSuperseded_KeepsNewRoomState(bool returnsDespiteCancellation)
    {
        var previousStatus = new TaskCompletionSource<IReadOnlySet<string>>();
        var previousToken = CancellationToken.None;
        var favourites = new Mock<IFavouritesService>();
        var readCount = 0;
        favourites.Setup(service => service.GetMyFavouriteIdsAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) =>
            {
                if (++readCount == 1)
                {
                    previousToken = token;
                    return previousStatus.Task;
                }

                return Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { "room-B" });
            });
        _sessionServiceMock.SetupGet(session => session.IsAuthenticated).Returns(true);
        _viewModel = new RoomDetailViewModel(
            _serviceMock.Object, _errorHandlerMock.Object, _sessionServiceMock.Object, favouritesService: favourites.Object);
        _serviceMock.Setup(service => service.GetRoomDetailAsync("room-A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoomDetailModel { Id = "room-A", SimilarRooms = [new SimilarRoomModel { Id = "similar-A" }] });
        _serviceMock.Setup(service => service.GetRoomDetailAsync("room-B", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoomDetailModel { Id = "room-B", SimilarRooms = [new SimilarRoomModel { Id = "similar-B" }] });

        _viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = "room-A" });
        var previousLoad = _viewModel.LoadRoomDetailsCommand.ExecutionTask!;
        Assert.That(readCount, Is.EqualTo(1));
        Assert.That(previousLoad.IsCompleted, Is.False);

        _viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = "room-B" });
        await _viewModel.LoadRoomDetailsCommand.ExecutionTask!;
        Assert.That(previousToken.IsCancellationRequested, Is.True);

        if (returnsDespiteCancellation)
        {
            previousStatus.SetResult(new HashSet<string>());
        }
        else
        {
            previousStatus.SetCanceled(previousToken);
        }

        await previousLoad;

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.Room?.Id, Is.EqualTo("room-B"));
            Assert.That(_viewModel.IsFavorite, Is.True);
            Assert.That(_viewModel.FavoriteErrorMessage, Is.Empty);
            Assert.That(_viewModel.SimilarRooms.Select(room => room.Id), Is.EqualTo(new[] { "similar-B" }));
            Assert.That(_viewModel.IsBusy, Is.False);
        });
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
            Assert.That(_viewModel.HasLoadError, Is.False);
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
            Assert.That(_viewModel.IsNotFound, Is.False);
            Assert.That(_viewModel.HasLoadError, Is.True);
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
