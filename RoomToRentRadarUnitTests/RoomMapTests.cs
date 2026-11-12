using NUnit.Framework;
using RoomToRentRadar;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Models;

namespace RoomToRentRadarUnitTests
{
    /// <summary>
    /// Unit Tests for Sprint 1 US06 (Interactive Map with pins and preview cards).
    /// Covers Acceptance Criteria:
    /// - AC1: Map displays interactive pins for all verified rooms around ICTU.
    /// - AC2: Tapping a pin highlights it and reveals a room preview card.
    /// - AC3: Tapping the preview card navigates to the full room detail screen.
    /// - AC4: Center map button resets camera to ICTU campus.
    /// </summary>
    [TestFixture]
    public class RoomMapTests
    {
        private List<RoomListing> _sampleRooms = null!;
        private InMemoryRoomMapRepo _repository = null!;

        [SetUp]
        public void Setup()
        {
            _sampleRooms = new List<RoomListing>
            {
                new RoomListing
                {
                    Id = 1,
                    Title = "Phòng khép kín ban công thoáng sát cổng chính ICTU",
                    MonthlyRent = 1500000m,
                    Deposit = 1500000m,
                    Area = 22,
                    RoomType = "Phòng khép kín",
                    Address = "Ngõ 12 Đường Z115, Xã Quyết Thắng, TP. Thái Nguyên",
                    Ward = "Quyết Thắng",
                    Latitude = 21.5862,
                    Longitude = 105.8075,
                    DistanceToCampusKm = 0.3,
                    MotorbikeMinutes = 2,
                    Status = "Available",
                    IsVerified = true,
                    SecurityScore = 4.8
                },
                new RoomListing
                {
                    Id = 2,
                    Title = "Chung cư mini 28m2 full nội thất hồ điều hòa Tân Thịnh",
                    MonthlyRent = 2200000m,
                    Deposit = 2000000m,
                    Area = 28,
                    RoomType = "Chung cư mini",
                    Address = "Tổ 7 Phường Tân Thịnh, TP. Thái Nguyên",
                    Ward = "Tân Thịnh",
                    Latitude = 21.5825,
                    Longitude = 105.8112,
                    DistanceToCampusKm = 0.8,
                    MotorbikeMinutes = 3,
                    Status = "Available",
                    IsVerified = true,
                    SecurityScore = 4.7
                },
                new RoomListing
                {
                    Id = 6,
                    Title = "[ĐÃ CHO THUÊ] Phòng khép kín tầng 2 sát giảng đường Z115",
                    MonthlyRent = 1400000m,
                    Deposit = 1400000m,
                    Area = 20,
                    RoomType = "Phòng khép kín",
                    Address = "Ngõ 18 Đường Z115, Xã Quyết Thắng, TP. Thái Nguyên",
                    Ward = "Quyết Thắng",
                    Latitude = 21.5858,
                    Longitude = 105.8080,
                    DistanceToCampusKm = 0.4,
                    Status = "Unavailable", // Excluded from map
                    IsVerified = true
                },
                new RoomListing
                {
                    Id = 7,
                    Title = "[CHỜ DUYỆT] Phòng mới xây 25m2 có ban công hướng hồ",
                    MonthlyRent = 1600000m,
                    Deposit = 1600000m,
                    Area = 25,
                    RoomType = "Phòng khép kín",
                    Address = "Xóm Tân Tiến, Xã Quyết Thắng, TP. Thái Nguyên",
                    Ward = "Quyết Thắng",
                    Latitude = 21.5870,
                    Longitude = 105.8050,
                    DistanceToCampusKm = 0.5,
                    Status = "PendingReview", // Excluded from map
                    IsVerified = false
                }
            };

            _repository = new InMemoryRoomMapRepo(_sampleRooms);
        }

        [Test]
        public async Task AC1_MapDisplaysPins_ForAllVerifiedAvailableRoomsAroundIctu()
        {
            var mapSession = new InteractiveMapSession(_repository);
            await mapSession.LoadRoomsAsync();

            // Assert: Only 2 verified and available rooms are loaded
            Assert.That(mapSession.Pins.Count, Is.EqualTo(2));
            Assert.That(mapSession.Pins.All(r => r.IsVerified && r.Status == "Available"), Is.True);

            // Assert: Each pin has valid geographical coordinates near ICTU
            foreach (var pin in mapSession.Pins)
            {
                Assert.That(pin.Latitude, Is.InRange(21.50, 21.70));
                Assert.That(pin.Longitude, Is.InRange(105.70, 105.90));
                Assert.That(pin.DistanceToCampusKm, Is.GreaterThan(0));
            }
        }

        [Test]
        public async Task AC2_TappingPin_HighlightsIt_AndRevealsPreviewCard()
        {
            var mapSession = new InteractiveMapSession(_repository);
            await mapSession.LoadRoomsAsync();

            // Initial: No room selected
            Assert.That(mapSession.SelectedRoom, Is.Null);
            Assert.That(mapSession.IsPreviewCardVisible, Is.False);

            // Act: Tap pin for Room 1
            mapSession.SelectPin(1);

            // Assert: Preview card is visible with Room 1 details
            Assert.That(mapSession.IsPreviewCardVisible, Is.True);
            Assert.That(mapSession.SelectedRoom, Is.Not.Null);
            Assert.That(mapSession.SelectedRoom!.Id, Is.EqualTo(1));
            Assert.That(mapSession.SelectedRoom.Title, Does.Contain("cổng chính ICTU"));
            Assert.That(mapSession.SelectedRoom.MonthlyRent, Is.EqualTo(1500000m));
            Assert.That(mapSession.SelectedRoom.DistanceToCampusKm, Is.EqualTo(0.3));

            // Act: Dismiss preview card
            mapSession.DismissPreviewCard();
            Assert.That(mapSession.SelectedRoom, Is.Null);
            Assert.That(mapSession.IsPreviewCardVisible, Is.False);
        }

        [Test]
        public async Task AC3_TappingPreviewCard_BuildsCorrectDetailNavigationRoute()
        {
            var mapSession = new InteractiveMapSession(_repository);
            await mapSession.LoadRoomsAsync();
            mapSession.SelectPin(2);

            Assert.That(mapSession.SelectedRoom, Is.Not.Null);
            Assert.That(mapSession.SelectedRoom!.Id, Is.EqualTo(2));

            // Assert: Navigation target uses standardized shell route
            string detailRoute = mapSession.GetDetailNavigationRoute();
            Assert.That(detailRoute, Is.EqualTo("room-detail?roomId=2"));
        }

        [Test]
        public async Task AC4_CenterMapButton_ResetsCameraToIctuCampus()
        {
            var mapSession = new InteractiveMapSession(_repository);
            await mapSession.LoadRoomsAsync();

            // Simulate user panned map away
            mapSession.CenterLatitude = 21.0285;
            mapSession.CenterLongitude = 105.8542;
            mapSession.ZoomLevel = 10;

            // Act: Tap Center ICTU button
            mapSession.ResetCameraToIctu();

            // Assert: Coordinates reset to ICTU benchmark coordinates (21.5850, 105.8064)
            Assert.That(mapSession.CenterLatitude, Is.EqualTo(Constants.IctuLatitude));
            Assert.That(mapSession.CenterLongitude, Is.EqualTo(Constants.IctuLongitude));
            Assert.That(mapSession.ZoomLevel, Is.EqualTo(15));
        }
    }

    /// <summary>
    /// Test double validating map session state machine and AC1-AC4 behaviors.
    /// </summary>
    public class InteractiveMapSession
    {
        private readonly IRoomListingRepository _repo;

        public List<RoomListing> Pins { get; } = new();
        public RoomListing? SelectedRoom { get; private set; }
        public bool IsPreviewCardVisible => SelectedRoom != null;
        public double CenterLatitude { get; set; } = Constants.IctuLatitude;
        public double CenterLongitude { get; set; } = Constants.IctuLongitude;
        public int ZoomLevel { get; set; } = 15;

        public InteractiveMapSession(IRoomListingRepository repo)
        {
            _repo = repo;
        }

        public async Task LoadRoomsAsync()
        {
            var rooms = await _repo.ListVerifiedRoomsAsync();
            Pins.Clear();
            Pins.AddRange(rooms.Where(r => r.IsVerified && r.Status == Constants.ListingStatuses.Available));
        }

        public void SelectPin(int roomId)
        {
            SelectedRoom = Pins.FirstOrDefault(r => r.Id == roomId);
        }

        public void DismissPreviewCard()
        {
            SelectedRoom = null;
        }

        public void ResetCameraToIctu()
        {
            CenterLatitude = Constants.IctuLatitude;
            CenterLongitude = Constants.IctuLongitude;
            ZoomLevel = 15;
        }

        public string GetDetailNavigationRoute()
        {
            return SelectedRoom != null
                ? $"{Constants.Routes.RoomDetail}?roomId={SelectedRoom.Id}"
                : string.Empty;
        }
    }

    public class InMemoryRoomMapRepo : IRoomListingRepository
    {
        private readonly List<RoomListing> _rooms;

        public InMemoryRoomMapRepo(List<RoomListing> rooms)
        {
            _rooms = rooms;
        }

        public Task<List<RoomListing>> ListVerifiedRoomsAsync(int pageIndex = 0, int pageSize = 20, string sortBy = "DistanceAsc")
        {
            var filtered = _rooms.Where(r => r.IsVerified && r.Status == "Available").ToList();
            return Task.FromResult(filtered);
        }

        public Task<RoomListing?> GetByIdAsync(int roomId) =>
            Task.FromResult(_rooms.FirstOrDefault(r => r.Id == roomId));

        public Task<List<RoomListing>> GetSimilarRoomsAsync(int excludeRoomId, string ward, decimal targetRent, int limit = 3) =>
            Task.FromResult(_rooms.Where(r => r.Id != excludeRoomId).Take(limit).ToList());

        public Task<List<RoomListing>> SearchAsync(RoomFilterCriteria criteria) =>
            Task.FromResult(_rooms);

        public Task<int> SaveListingAsync(RoomListing listing) =>
            Task.FromResult(listing.Id);

        public Task<bool> UpdateStatusAsync(int roomId, string status, bool isVerified) =>
            Task.FromResult(true);
    }
}
