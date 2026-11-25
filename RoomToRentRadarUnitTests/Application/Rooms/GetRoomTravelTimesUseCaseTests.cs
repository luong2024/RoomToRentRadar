using NSubstitute;
using RoomToRentRadar.Application.UseCases.Rooms;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Rooms;

/// <summary>
/// US09 / T09.1 / UC-12 / BR-TRV-01..04: travel time calculation, 30-day cache,
/// Bus estimation formula, profile campus resolution, and straight-line fallback.
/// </summary>
[TestFixture]
public class GetRoomTravelTimesUseCaseTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Campus Ictu = new("ICTU", "ĐH CNTT & TT", "ICTU", 21.5851, 105.8066, isDefault: true, sortOrder: 1);
    private static readonly Campus Tnus = new("TNUS", "ĐH Khoa học", "TNUS", 21.58870, 105.80979, isDefault: false, sortOrder: 2);

    private IRoomRepository _roomRepository = null!;
    private IUserRepository _userRepository = null!;
    private ICampusRepository _campusRepository = null!;
    private ICurrentUserContextPort _currentUserContext = null!;
    private ITravelTimeCacheRepository _cacheRepository = null!;
    private ITravelTimePort _travelTimePort = null!;
    private IClock _clock = null!;
    private GetRoomTravelTimesUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _roomRepository = Substitute.For<IRoomRepository>();
        _userRepository = Substitute.For<IUserRepository>();
        _campusRepository = Substitute.For<ICampusRepository>();
        _currentUserContext = Substitute.For<ICurrentUserContextPort>();
        _cacheRepository = Substitute.For<ITravelTimeCacheRepository>();
        _travelTimePort = Substitute.For<ITravelTimePort>();
        _clock = Substitute.For<IClock>();

        _clock.UtcNow.Returns(FixedNow);
        _campusRepository.GetDefaultCampusAsync(Arg.Any<CancellationToken>()).Returns(Ictu);
        _campusRepository.GetByIdAsync("ICTU", Arg.Any<CancellationToken>()).Returns(Ictu);
        _campusRepository.GetByIdAsync("TNUS", Arg.Any<CancellationToken>()).Returns(Tnus);
        _campusRepository.GetByIdAsync(Arg.Is<string>(id => id != "ICTU" && id != "TNUS"), Arg.Any<CancellationToken>())
            .Returns((Campus?)null);

        _cacheRepository.GetByRoomAndCampusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TravelTimeCacheEntry>());

        _sut = new GetRoomTravelTimesUseCase(
            _roomRepository,
            _userRepository,
            _campusRepository,
            _currentUserContext,
            _cacheRepository,
            _travelTimePort,
            _clock);
    }

    [Test]
    public async Task ExecuteAsync_CacheMiss_RoutesComputesBusCachesAndReturnsAllThreeModes()
    {
        var room = SampleRoom();
        _roomRepository.GetByIdAsync("room-1", Arg.Any<CancellationToken>()).Returns(room);
        _travelTimePort.GetRoutesAsync(room.Latitude, room.Longitude, Ictu.Latitude, Ictu.Longitude, Arg.Any<CancellationToken>())
            .Returns(new RoutingModesResult(
                Driving: new RouteLegResult(DistanceMeters: 1850, DurationSeconds: 300),
                Walking: new RouteLegResult(DistanceMeters: 1620, DurationSeconds: 1180)));

        var result = await _sut.ExecuteAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(result.CampusId, Is.EqualTo("ICTU"));
            Assert.That(result.IsFallback, Is.False);
            Assert.That(result.StraightLineDistanceKm, Is.EqualTo(
                HaversineDistanceCalculator.CalculateDistanceKm(Ictu.Latitude, Ictu.Longitude, room.Latitude, room.Longitude)));
            Assert.That(result.Items, Has.Count.EqualTo(3));

            Assert.That(result.Items[0].Mode, Is.EqualTo(TravelMode.Motorbike));
            Assert.That(result.Items[0].DistanceMeters, Is.EqualTo(1850));
            Assert.That(result.Items[0].DurationSeconds, Is.EqualTo(300));
            Assert.That(result.Items[0].IsEstimate, Is.False);

            // BR-TRV-02: Bus = driving duration * 1.6 + 10 min (600s) = 300 * 1.6 + 600 = 1080
            Assert.That(result.Items[1].Mode, Is.EqualTo(TravelMode.Bus));
            Assert.That(result.Items[1].DistanceMeters, Is.EqualTo(1850));
            Assert.That(result.Items[1].DurationSeconds, Is.EqualTo(1080));
            Assert.That(result.Items[1].IsEstimate, Is.True);

            Assert.That(result.Items[2].Mode, Is.EqualTo(TravelMode.Walking));
            Assert.That(result.Items[2].DistanceMeters, Is.EqualTo(1620));
            Assert.That(result.Items[2].DurationSeconds, Is.EqualTo(1180));
            Assert.That(result.Items[2].IsEstimate, Is.False);
        });

        await _cacheRepository.Received(1).UpsertRangeAsync(
            Arg.Is<IReadOnlyList<TravelTimeCacheEntry>>(entries =>
                entries.Count == 3
                && entries.All(e => e.RoomId == "room-1" && e.CampusId == "ICTU" && e.ComputedAtUtc == FixedNow)
                && entries.Single(e => e.Mode == TravelMode.Bus).Source == "Estimated"
                && entries.Single(e => e.Mode == TravelMode.Motorbike).Source == "Routing"
                && entries.Single(e => e.Mode == TravelMode.Walking).Source == "Routing"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_ValidCacheHitWithin30Days_ReturnsCachedWithoutCallingRoutingPort()
    {
        var room = SampleRoom();
        _roomRepository.GetByIdAsync("room-1", Arg.Any<CancellationToken>()).Returns(room);
        var cachedAt = FixedNow.AddDays(-29);
        _cacheRepository.GetByRoomAndCampusAsync("room-1", "ICTU", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new TravelTimeCacheEntry("room-1", "ICTU", TravelMode.Motorbike, 1700, 290, "Routing", cachedAt),
                new TravelTimeCacheEntry("room-1", "ICTU", TravelMode.Bus, 1700, 1064, "Estimated", cachedAt),
                new TravelTimeCacheEntry("room-1", "ICTU", TravelMode.Walking, 1500, 1100, "Routing", cachedAt)
            });

        var result = await _sut.ExecuteAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFallback, Is.False);
            Assert.That(result.Items, Has.Count.EqualTo(3));
            Assert.That(result.Items[0].DurationSeconds, Is.EqualTo(290));
            Assert.That(result.Items[1].IsEstimate, Is.True);
        });

        await _travelTimePort.DidNotReceiveWithAnyArgs().GetRoutesAsync(default, default, default, default, default);
        await _cacheRepository.DidNotReceiveWithAnyArgs().UpsertRangeAsync(default!, default);
    }

    [Test]
    public async Task ExecuteAsync_CacheExpired30Days_RefreshesFromRoutingPortAndUpserts()
    {
        var room = SampleRoom();
        _roomRepository.GetByIdAsync("room-1", Arg.Any<CancellationToken>()).Returns(room);
        var expiredAt = FixedNow.AddDays(-30).AddSeconds(-1);
        _cacheRepository.GetByRoomAndCampusAsync("room-1", "ICTU", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new TravelTimeCacheEntry("room-1", "ICTU", TravelMode.Motorbike, 1000, 100, "Routing", expiredAt),
                new TravelTimeCacheEntry("room-1", "ICTU", TravelMode.Bus, 1000, 760, "Estimated", expiredAt),
                new TravelTimeCacheEntry("room-1", "ICTU", TravelMode.Walking, 900, 600, "Routing", expiredAt)
            });
        _travelTimePort.GetRoutesAsync(room.Latitude, room.Longitude, Ictu.Latitude, Ictu.Longitude, Arg.Any<CancellationToken>())
            .Returns(new RoutingModesResult(
                new RouteLegResult(2000, 400),
                new RouteLegResult(1800, 1250)));

        var result = await _sut.ExecuteAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFallback, Is.False);
            Assert.That(result.Items.Single(x => x.Mode == TravelMode.Motorbike).DurationSeconds, Is.EqualTo(400));
            Assert.That(result.Items.Single(x => x.Mode == TravelMode.Bus).DurationSeconds, Is.EqualTo(1240));
        });
        await _travelTimePort.Received(1).GetRoutesAsync(room.Latitude, room.Longitude, Ictu.Latitude, Ictu.Longitude, Arg.Any<CancellationToken>());
        await _cacheRepository.Received(1).UpsertRangeAsync(Arg.Any<IReadOnlyList<TravelTimeCacheEntry>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_RoutingUnavailable_ReturnsStraightLineFallbackAndDoesNotCache()
    {
        var room = SampleRoom();
        _roomRepository.GetByIdAsync("room-1", Arg.Any<CancellationToken>()).Returns(room);
        _travelTimePort.GetRoutesAsync(room.Latitude, room.Longitude, Ictu.Latitude, Ictu.Longitude, Arg.Any<CancellationToken>())
            .Returns((RoutingModesResult?)null);

        var result = await _sut.ExecuteAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(result.CampusId, Is.EqualTo("ICTU"));
            Assert.That(result.IsFallback, Is.True);
            Assert.That(result.Items, Is.Empty);
            Assert.That(result.StraightLineDistanceKm, Is.EqualTo(
                HaversineDistanceCalculator.CalculateDistanceKm(Ictu.Latitude, Ictu.Longitude, room.Latitude, room.Longitude)));
        });

        await _cacheRepository.DidNotReceiveWithAnyArgs().UpsertRangeAsync(default!, default);
    }

    [Test]
    public async Task ExecuteAsync_AuthenticatedUserWithoutQueryCampus_UsesProfileSelectedCampus()
    {
        var room = SampleRoom();
        var student = SampleUser("student-1", selectedCampusId: "TNUS");
        _roomRepository.GetByIdAsync("room-1", Arg.Any<CancellationToken>()).Returns(room);
        _currentUserContext.IsAuthenticated.Returns(true);
        _currentUserContext.UserId.Returns("student-1");
        _userRepository.GetByIdAsync("student-1", Arg.Any<CancellationToken>()).Returns(student);
        _travelTimePort.GetRoutesAsync(room.Latitude, room.Longitude, Tnus.Latitude, Tnus.Longitude, Arg.Any<CancellationToken>())
            .Returns(new RoutingModesResult(new RouteLegResult(2100, 360), new RouteLegResult(1900, 1300)));

        var result = await _sut.ExecuteAsync("room-1", campusId: null);

        Assert.That(result.CampusId, Is.EqualTo("TNUS"));
        await _travelTimePort.Received(1).GetRoutesAsync(room.Latitude, room.Longitude, Tnus.Latitude, Tnus.Longitude, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_ExplicitCampusOverridesProfileCampus()
    {
        var room = SampleRoom();
        var student = SampleUser("student-1", selectedCampusId: "TNUS");
        _roomRepository.GetByIdAsync("room-1", Arg.Any<CancellationToken>()).Returns(room);
        _currentUserContext.IsAuthenticated.Returns(true);
        _currentUserContext.UserId.Returns("student-1");
        _userRepository.GetByIdAsync("student-1", Arg.Any<CancellationToken>()).Returns(student);
        _travelTimePort.GetRoutesAsync(room.Latitude, room.Longitude, Ictu.Latitude, Ictu.Longitude, Arg.Any<CancellationToken>())
            .Returns((RoutingModesResult?)null);

        var result = await _sut.ExecuteAsync("room-1", campusId: "ICTU");

        Assert.That(result.CampusId, Is.EqualTo("ICTU"));
    }

    [Test]
    public async Task ExecuteAsync_UnknownCampus_ThrowsCampusNotFound()
    {
        var room = SampleRoom();
        _roomRepository.GetByIdAsync("room-1", Arg.Any<CancellationToken>()).Returns(room);

        var ex = Assert.ThrowsAsync<DomainValidationException>(async () =>
            await _sut.ExecuteAsync("room-1", campusId: "INVALID"));

        Assert.That(ex!.ErrorCode, Is.EqualTo(ProblemCodes.CampusNotFound));
    }

    [Test]
    public async Task ExecuteAsync_RoomNotFoundOrHiddenForGuest_ThrowsRoomNotFound()
    {
        _roomRepository.GetByIdAsync("missing", Arg.Any<CancellationToken>()).Returns((Room?)null);
        var exMissing = Assert.ThrowsAsync<EntityNotFoundException>(async () => await _sut.ExecuteAsync("missing"));
        Assert.That(exMissing!.ErrorCode, Is.EqualTo(ProblemCodes.RoomNotFound));

        var hiddenRoom = SampleRoom(status: ListingStatus.PendingReview);
        _roomRepository.GetByIdAsync("room-1", Arg.Any<CancellationToken>()).Returns(hiddenRoom);
        _currentUserContext.IsAuthenticated.Returns(false);

        var exHidden = Assert.ThrowsAsync<EntityNotFoundException>(async () => await _sut.ExecuteAsync("room-1"));
        Assert.That(exHidden!.ErrorCode, Is.EqualTo(ProblemCodes.RoomNotFound));
    }

    [Test]
    public async Task ExecuteAsync_HiddenRoomForOwner_Succeeds()
    {
        var hiddenRoom = SampleRoom(status: ListingStatus.PendingReview, landlordId: "owner-1");
        _roomRepository.GetByIdAsync("room-1", Arg.Any<CancellationToken>()).Returns(hiddenRoom);
        _currentUserContext.IsAuthenticated.Returns(true);
        _currentUserContext.UserId.Returns("owner-1");
        _travelTimePort.GetRoutesAsync(hiddenRoom.Latitude, hiddenRoom.Longitude, Ictu.Latitude, Ictu.Longitude, Arg.Any<CancellationToken>())
            .Returns((RoutingModesResult?)null);

        var result = await _sut.ExecuteAsync("room-1");
        Assert.That(result.CampusId, Is.EqualTo("ICTU"));
    }

    private static Room SampleRoom(ListingStatus status = ListingStatus.Available, string landlordId = "landlord-1") =>
        Room.Reconstitute(
            id: "room-1",
            landlordId: landlordId,
            title: "Phòng trọ gần cổng ICTU",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: 1_800_000L,
            depositVnd: 1_800_000L,
            areaM2: 22.0,
            addressLine: "Đường Z115",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5875,
            longitude: 105.8080,
            status: status,
            createdAtUtc: FixedNow,
            lastUpdatedAtUtc: FixedNow,
            version: 1);

    private static User SampleUser(string id, string selectedCampusId) =>
        User.Reconstitute(
            id: id,
            email: $"{id}@ictu.edu.vn",
            fullName: "Sinh Viên",
            passwordHash: "hash",
            role: UserRole.Student,
            authProvider: AuthProvider.Local,
            ssoSubjectId: null,
            phoneNumber: null,
            studentCode: "DTC001",
            isVerified: true,
            failedLoginCount: 0,
            lockoutEndUtc: null,
            createdAtUtc: FixedNow,
            updatedAtUtc: FixedNow,
            refreshTokens: null,
            selectedCampusId: selectedCampusId,
            preferredLanguage: "vi",
            version: 1);
}
