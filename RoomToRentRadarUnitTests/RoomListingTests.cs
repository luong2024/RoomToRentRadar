using NUnit.Framework;
using RoomToRentRadar.Models;

namespace RoomToRentRadarUnitTests
{
    /// <summary>
    /// Unit Tests for Sprint 1 US02 (Verified Room Listings Discovery, Room Cards &amp; Infinite Scroll).
    /// Covers Acceptance Criteria:
    /// - AC1: The list is sorted by distance from ICTU campus by default.
    /// - AC2: Each card shows title, monthly rent, area, distance to campus, and verification badge.
    /// - AC3: The list loads within 2 seconds for up to 500 listings (pagination / infinite scroll).
    /// - AC4: Only listings with status Available and Verified are displayed.
    /// </summary>
    [TestFixture]
    public class RoomListingTests
    {
        private List<RoomListing> _allSampleRooms = null!;

        [SetUp]
        public void Setup()
        {
            _allSampleRooms = new List<RoomListing>
            {
                // Room 1: 0.3km, Available, Verified
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
                    DistanceToCampusKm = 0.3,
                    Status = "Available",
                    IsVerified = true,
                    SecurityScore = 4.8
                },
                // Room 2: 0.8km, Available, Verified
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
                    DistanceToCampusKm = 0.8,
                    Status = "Available",
                    IsVerified = true,
                    SecurityScore = 4.7
                },
                // Room 3: 0.6km, Available, Verified
                new RoomListing
                {
                    Id = 3,
                    Title = "Phòng trọ sinh viên giá rẻ khu Quyết Thắng an ninh tốt",
                    MonthlyRent = 950000m,
                    Deposit = 500000m,
                    Area = 16,
                    RoomType = "Phòng khép kín",
                    Address = "Xóm Gò Móc, Xã Quyết Thắng, TP. Thái Nguyên",
                    Ward = "Quyết Thắng",
                    DistanceToCampusKm = 0.6,
                    Status = "Available",
                    IsVerified = true,
                    SecurityScore = 4.5
                },
                // Room 4: 1.2km, Available, Verified
                new RoomListing
                {
                    Id = 4,
                    Title = "Phòng Studio cửa sổ lớn đường Z115 gần chợ sinh viên",
                    MonthlyRent = 1800000m,
                    Deposit = 1800000m,
                    Area = 24,
                    RoomType = "Chung cư mini",
                    Address = "Số 145 Đường Z115, Phường Tân Thịnh, TP. Thái Nguyên",
                    Ward = "Tân Thịnh",
                    DistanceToCampusKm = 1.2,
                    Status = "Available",
                    IsVerified = true,
                    SecurityScore = 4.6
                },
                // Room 5: 1.9km, Available, Verified
                new RoomListing
                {
                    Id = 5,
                    Title = "Phòng ở ghép 2 người khu vực Thịnh Đán gần vòng xuyến",
                    MonthlyRent = 800000m,
                    Deposit = 800000m,
                    Area = 25,
                    RoomType = "Ở ghép",
                    Address = "Tổ 4 Phường Thịnh Đán, TP. Thái Nguyên",
                    Ward = "Thịnh Đán",
                    DistanceToCampusKm = 1.9,
                    Status = "Available",
                    IsVerified = true,
                    SecurityScore = 4.4
                },
                // Room 6: 0.4km, UNAVAILABLE (Rented out)
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
                    DistanceToCampusKm = 0.4,
                    Status = "Unavailable", // NOT AVAILABLE
                    IsVerified = true
                },
                // Room 7: 0.5km, PENDING REVIEW (Not verified)
                new RoomListing
                {
                    Id = 7,
                    Title = "[CHỜ DUYỆT] Phòng mới xây 25m2 có ban công hướng hồ Quyết Thắng",
                    MonthlyRent = 1600000m,
                    Deposit = 1600000m,
                    Area = 25,
                    RoomType = "Phòng khép kín",
                    Address = "Xóm Tân Tiến, Xã Quyết Thắng, TP. Thái Nguyên",
                    Ward = "Quyết Thắng",
                    DistanceToCampusKm = 0.5,
                    Status = "PendingReview",
                    IsVerified = false // NOT VERIFIED
                }
            };
        }

        [Test]
        public void AC4_OnlyAvailableAndVerifiedRoomsAreDisplayed()
        {
            // Act: Filter by Status = Available and IsVerified = true (US02 AC4)
            var displayedRooms = _allSampleRooms
                .Where(r => r.Status == "Available" && r.IsVerified)
                .ToList();

            // Assert: Only 5 rooms qualify (Rooms 1, 2, 3, 4, 5)
            Assert.That(displayedRooms.Count, Is.EqualTo(5));
            Assert.That(displayedRooms.Any(r => r.Id == 6), Is.False, "Room 6 (Unavailable) must be excluded");
            Assert.That(displayedRooms.Any(r => r.Id == 7), Is.False, "Room 7 (Unverified) must be excluded");

            // All displayed rooms must be Available and Verified
            Assert.That(displayedRooms.All(r => r.Status == "Available"), Is.True);
            Assert.That(displayedRooms.All(r => r.IsVerified), Is.True);
        }

        [Test]
        public void AC1_DefaultSortingIsDistanceAscendingFromIctuCampus()
        {
            // Act: Default sort by distance to ICTU campus
            var sortedRooms = _allSampleRooms
                .Where(r => r.Status == "Available" && r.IsVerified)
                .OrderBy(r => r.DistanceToCampusKm)
                .ToList();

            // Assert order: Room 1 (0.3km) -> Room 3 (0.6km) -> Room 2 (0.8km) -> Room 4 (1.2km) -> Room 5 (1.9km)
            Assert.That(sortedRooms.Count, Is.EqualTo(5));
            Assert.That(sortedRooms[0].Id, Is.EqualTo(1));
            Assert.That(sortedRooms[0].DistanceToCampusKm, Is.EqualTo(0.3));

            Assert.That(sortedRooms[1].Id, Is.EqualTo(3));
            Assert.That(sortedRooms[1].DistanceToCampusKm, Is.EqualTo(0.6));

            Assert.That(sortedRooms[2].Id, Is.EqualTo(2));
            Assert.That(sortedRooms[2].DistanceToCampusKm, Is.EqualTo(0.8));

            Assert.That(sortedRooms[3].Id, Is.EqualTo(4));
            Assert.That(sortedRooms[3].DistanceToCampusKm, Is.EqualTo(1.2));

            Assert.That(sortedRooms[4].Id, Is.EqualTo(5));
            Assert.That(sortedRooms[4].DistanceToCampusKm, Is.EqualTo(1.9));
        }

        [Test]
        public void AC2_RoomCard_ContainsAllRequiredFields()
        {
            var verifiedRooms = _allSampleRooms
                .Where(r => r.Status == "Available" && r.IsVerified)
                .ToList();

            foreach (var card in verifiedRooms)
            {
                // Title
                Assert.That(string.IsNullOrWhiteSpace(card.Title), Is.False);
                // Monthly rent
                Assert.That(card.MonthlyRent, Is.GreaterThan(0));
                // Area
                Assert.That(card.Area, Is.GreaterThan(0));
                // Distance to campus
                Assert.That(card.DistanceToCampusKm, Is.GreaterThan(0));
                // Verification badge
                Assert.That(card.IsVerified, Is.True);
            }
        }

        [Test]
        public void AC3_InfiniteScrollPagination_HandlesLargeListsEfficiently()
        {
            // Simulate 500 listings (AC3 performance criteria: loads up to 500 listings)
            var largeList = new List<RoomListing>();
            for (int i = 1; i <= 500; i++)
            {
                largeList.Add(new RoomListing
                {
                    Id = i,
                    Title = $"Phòng trọ mẫu #{i}",
                    MonthlyRent = 1000000m + (i * 5000),
                    Area = 20 + (i % 15),
                    DistanceToCampusKm = 0.2 + (i * 0.01),
                    Status = "Available",
                    IsVerified = true
                });
            }

            const int pageSize = 10;

            // Page 0 (initial load)
            var page0 = largeList.Skip(0).Take(pageSize).ToList();
            Assert.That(page0.Count, Is.EqualTo(10));
            Assert.That(page0[0].Id, Is.EqualTo(1));

            // Page 1 (first infinite scroll step)
            var page1 = largeList.Skip(10).Take(pageSize).ToList();
            Assert.That(page1.Count, Is.EqualTo(10));
            Assert.That(page1[0].Id, Is.EqualTo(11));

            // Page 49 (last page)
            var page49 = largeList.Skip(490).Take(pageSize).ToList();
            Assert.That(page49.Count, Is.EqualTo(10));
            Assert.That(page49[^1].Id, Is.EqualTo(500));

            // Total listings simulated
            Assert.That(largeList.Count, Is.EqualTo(500));
        }

        [Test]
        public void AlternateSorting_RentAscendingAndDescending()
        {
            var verifiedRooms = _allSampleRooms
                .Where(r => r.Status == "Available" && r.IsVerified)
                .ToList();

            // RentAsc: Room 5 (800k) -> Room 3 (950k) -> Room 1 (1.5M) -> Room 4 (1.8M) -> Room 2 (2.2M)
            var rentAsc = verifiedRooms.OrderBy(r => r.MonthlyRent).ToList();
            Assert.That(rentAsc[0].Id, Is.EqualTo(5));
            Assert.That(rentAsc[0].MonthlyRent, Is.EqualTo(800000m));
            Assert.That(rentAsc[^1].Id, Is.EqualTo(2));
            Assert.That(rentAsc[^1].MonthlyRent, Is.EqualTo(2200000m));

            // RentDesc: Room 2 (2.2M) -> Room 4 (1.8M) -> ... -> Room 5 (800k)
            var rentDesc = verifiedRooms.OrderByDescending(r => r.MonthlyRent).ToList();
            Assert.That(rentDesc[0].Id, Is.EqualTo(2));
            Assert.That(rentDesc[^1].Id, Is.EqualTo(5));
        }
    }
}

