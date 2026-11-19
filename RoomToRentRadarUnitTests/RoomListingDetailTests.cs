using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using RoomToRentRadar;
using RoomToRentRadar.Data;
using RoomToRentRadar.Infrastructure.Persistence;
using RoomToRentRadar.Models;

namespace RoomToRentRadarUnitTests
{
    /// <summary>
    /// Comprehensive test suite for US03 / Task T03.1: Build room detail API (description, amenities, rules).
    /// Tests GetByIdAsync and GetSimilarRoomsAsync SQLite queries, edge cases, and performance.
    /// Acceptance Criteria:
    ///   AC1: Details include description, rent, deposit, area, room type, amenities, address, coordinates, travel times.
    ///   AC2: The screen shows the listing status and last-updated date.
    ///   AC3: If the listing is unavailable, the app shows a message and suggests similar rooms.
    ///   AC4: Landlord phone number is masked for guests and unmasked only for signed-in users.
    /// </summary>
    [TestFixture]
    [CancelAfter(5000)]
    public class RoomListingDetailTests
    {
        private string _tempDbPath = null!;
        private RentalDatabaseService _databaseService = null!;
        private RoomListingRepository _repository = null!;

        [SetUp]
        public async Task SetUp()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"rtr_detail_test_{Guid.NewGuid():N}.db3");
            var options = new DatabaseOptions($"Data Source={_tempDbPath}");
            _databaseService = new RentalDatabaseService(NullLogger<RentalDatabaseService>.Instance, options);
            await _databaseService.EnsureInitializedAsync();
            _repository = new RoomListingRepository(_databaseService);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_tempDbPath) && File.Exists(_tempDbPath))
            {
                try { File.Delete(_tempDbPath); } catch { }
            }
        }

        [Test]
        public async Task GetByIdAsync_ExistingRoomId_ReturnsFullDetailsWithAllMetadataSections()
        {
            // AC1: Full details including description, rent, deposit, area, room type, amenities, address, coordinates, travel times
            var room = await _repository.GetByIdAsync(1);

            Assert.That(room, Is.Not.Null);
            Assert.That(room!.Id, Is.EqualTo(1));
            Assert.That(room.Title, Is.Not.Null.And.Not.Empty);
            Assert.That(room.Description, Is.Not.Null.And.Not.Empty);
            Assert.That(room.MonthlyRent, Is.GreaterThan(0));
            Assert.That(room.Deposit, Is.GreaterThanOrEqualTo(0));
            Assert.That(room.Area, Is.GreaterThan(0));
            Assert.That(room.RoomType, Is.Not.Null.And.Not.Empty);
            Assert.That(room.Address, Is.Not.Null.And.Not.Empty);
            Assert.That(room.Ward, Is.Not.Null.And.Not.Empty);

            // Coordinates & Proximity
            Assert.That(room.Latitude, Is.GreaterThan(0));
            Assert.That(room.Longitude, Is.GreaterThan(0));
            Assert.That(room.DistanceToCampusKm, Is.GreaterThan(0));

            // Travel time estimates
            Assert.That(room.MotorbikeMinutes, Is.GreaterThan(0));
            Assert.That(room.BusMinutes, Is.GreaterThan(0));
            Assert.That(room.WalkMinutes, Is.GreaterThan(0));

            // Media & Amenities
            Assert.That(room.AmenitiesCsv, Is.Not.Null.And.Not.Empty);
            Assert.That(room.ThumbnailUrl, Is.Not.Null.And.Not.Empty);
            Assert.That(room.PhotosJson, Is.Not.Null.And.Not.Empty);

            // Rules & Security
            Assert.That(room.SecurityScore, Is.GreaterThan(0));
            Assert.That(room.QuietHours, Is.Not.Null.And.Not.Empty);
            Assert.That(room.CookingRules, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GetByIdAsync_ReturnsCorrectStatusAndUpdatedAtDate()
        {
            // AC2: Shows listing status and last-updated date
            var room = await _repository.GetByIdAsync(1);

            Assert.That(room, Is.Not.Null);
            Assert.That(room!.Status, Is.EqualTo(Constants.ListingStatuses.Available));
            Assert.That(room.UpdatedAt, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GetByIdAsync_NonExistingOrInvalidRoomId_ReturnsNull()
        {
            var notFound = await _repository.GetByIdAsync(99999);
            var invalidZero = await _repository.GetByIdAsync(0);
            var invalidNegative = await _repository.GetByIdAsync(-5);

            Assert.That(notFound, Is.Null);
            Assert.That(invalidZero, Is.Null);
            Assert.That(invalidNegative, Is.Null);
        }

        [Test]
        public async Task GetByIdAsync_LandlordPhone_PreservesRawNumberAndProvidesMaskedForGuests()
        {
            // AC4: Landlord phone is masked for guests and unmasked only for signed-in users
            var room = await _repository.GetByIdAsync(1);

            Assert.That(room, Is.Not.Null);
            Assert.That(room!.LandlordPhone, Is.Not.Null.And.Not.Empty);
            Assert.That(room.LandlordName, Is.Not.Null.And.Not.Empty);

            // Verify masked phone format for guest view
            string masked = room.MaskedLandlordPhone;
            Assert.That(masked, Does.Contain("***"));
            Assert.That(masked, Is.Not.EqualTo(room.LandlordPhone));
            Assert.That(masked.Length, Is.GreaterThanOrEqualTo(7));
        }

        [Test]
        public async Task GetSimilarRoomsAsync_ExcludesCurrentRoomId_AndOnlyReturnsAvailableVerified()
        {
            // AC3: Suggests similar rooms, excluding current room and only available/verified
            int excludeId = 1;
            var similar = await _repository.GetSimilarRoomsAsync(excludeId, "Quyết Thắng", 1500000m, limit: 3);

            Assert.That(similar, Is.Not.Null);
            Assert.That(similar.Count, Is.GreaterThan(0));
            Assert.That(similar.All(r => r.Id != excludeId), Is.True, "Must exclude current room.");
            Assert.That(similar.All(r => r.Status == Constants.ListingStatuses.Available && r.IsVerified), Is.True,
                "Must only recommend Available and Verified rooms.");
        }

        [Test]
        public async Task GetSimilarRoomsAsync_PrioritizesSameWardAndClosestRentPrice()
        {
            // AC3: Smart ranking: same ward prioritized, followed by closest rent
            string targetWard = "Quyết Thắng";
            decimal targetRent = 1800000m;

            var similar = await _repository.GetSimilarRoomsAsync(excludeRoomId: 1, ward: targetWard, targetRent: targetRent, limit: 3);

            Assert.That(similar, Is.Not.Null);
            Assert.That(similar.Count, Is.GreaterThan(0));

            // First result should match target ward if available
            var first = similar.First();
            Assert.That(string.Equals(first.Ward, targetWard, StringComparison.OrdinalIgnoreCase), Is.True,
                "Rooms in the same ward should be prioritized first.");
        }

        [Test]
        public async Task GetSimilarRoomsAsync_RespectsLimitParameter()
        {
            var limit1 = await _repository.GetSimilarRoomsAsync(excludeRoomId: 1, ward: "Quyết Thắng", targetRent: 1500000m, limit: 1);
            var limit2 = await _repository.GetSimilarRoomsAsync(excludeRoomId: 1, ward: "Quyết Thắng", targetRent: 1500000m, limit: 2);

            Assert.That(limit1.Count, Is.EqualTo(1));
            Assert.That(limit2.Count, Is.LessThanOrEqualTo(2));
        }

        [Test]
        public async Task GetByIdAsync_Performance_UnderTwoSecondsForMultipleLookups()
        {
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 50; i++)
            {
                await _repository.GetByIdAsync(1);
            }
            stopwatch.Stop();

            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(2000), "50 detail lookups should complete in under 2 seconds.");
        }
    }
}
