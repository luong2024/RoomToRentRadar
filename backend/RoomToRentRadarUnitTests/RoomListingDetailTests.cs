using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Data;
using RoomToRentRadar.Infrastructure.DependencyInjection;

namespace RoomToRentRadarUnitTests
{
    /// <summary>
    /// T03.1 (US03: Room detail API) — data side, against a real temporary SQLite file with the ICTU seed:
    /// room 1 Quyết Thắng 1.5M (Available), 3 Quyết Thắng 950k, 4 Tân Thịnh 1.8M, 2 Tân Thịnh 2.2M,
    /// 5 Thịnh Đán 800k, 6 Quyết Thắng 1.4M (Unavailable), 7 Quyết Thắng 1.6M (PendingReview, not verified).
    /// Phone masking (AC4) is done by RoomDetailController and checked with RoomToRentRadar.Api.http.
    /// </summary>
    [TestFixture]
    [CancelAfter(5000)]
    public class RoomListingDetailTests
    {
        private ServiceProvider _provider = null!;
        private string _tempDbPath = null!;
        private IRoomListingRepository _repo = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"rtr_t031_{Guid.NewGuid():N}.db3");

            var services = new ServiceCollection();
            services.AddRentalInfrastructure($"Data Source={_tempDbPath}");
            services.AddSingleton<IRoomListingRepository, RoomListingRepository>();
            _provider = services.BuildServiceProvider();
            _repo = _provider.GetRequiredService<IRoomListingRepository>();
        }

        [TearDown]
        public void TearDown()
        {
            _provider?.Dispose();
            if (!string.IsNullOrEmpty(_tempDbPath) && File.Exists(_tempDbPath))
            {
                try { File.Delete(_tempDbPath); } catch (IOException) { /* SQLite may still hold the file */ }
            }
        }

        [Test]
        public async Task GetByIdAsync_AC1_AC2_ReturnsEveryDetailSectionFromSqlite()
        {
            var room = await _repo.GetByIdAsync(1);

            Assert.That(room, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(room!.Id, Is.EqualTo(1));
                Assert.That(room.Description, Is.Not.Empty, "AC1 description");
                Assert.That(room.MonthlyRent, Is.EqualTo(1500000m), "AC1 rent");
                Assert.That(room.Deposit, Is.EqualTo(1500000m), "AC1 deposit");
                Assert.That(room.Area, Is.GreaterThan(0), "AC1 area");
                Assert.That(room.RoomType, Is.Not.Empty, "AC1 room type");
                Assert.That(room.AmenitiesCsv, Does.Contain("Wifi cáp quang"), "AC1 amenities");
                Assert.That(room.Address, Is.Not.Empty, "AC1 address");
                Assert.That(room.LandlordName, Is.Not.Empty, "AC1 landlord contact");
                Assert.That(room.LandlordPhone, Is.EqualTo("0912333444"), "AC1 landlord phone (masked later by the API)");
                Assert.That(room.QuietHours, Is.Not.Empty, "AC1 house rules");
                Assert.That(room.GuestPolicy, Is.Not.Empty, "AC1 house rules");
                Assert.That(room.Status, Is.EqualTo(Constants.ListingStatuses.Available), "AC2 status");
                Assert.That(room.UpdatedAt, Is.Not.Empty, "AC2 last-updated date");
            });
        }

        [Test]
        public async Task GetByIdAsync_AC3_ReturnsUnavailableRoomSoTheApiCanExplainIt()
        {
            var room = await _repo.GetByIdAsync(6);

            Assert.That(room, Is.Not.Null, "An unavailable room must still be found (AC3 message + suggestions)");
            Assert.That(room!.Status, Is.EqualTo(Constants.ListingStatuses.Unavailable));
        }

        [TestCase(0)]
        [TestCase(-5)]
        [TestCase(9999)]
        public async Task GetByIdAsync_UnknownOrInvalidId_ReturnsNull(int roomId)
        {
            var room = await _repo.GetByIdAsync(roomId);

            Assert.That(room, Is.Null);
        }

        [Test]
        public async Task GetSimilarRoomsAsync_AC3_SameWardFirstThenClosestRent()
        {
            // Room 6: Quyết Thắng, 1.4M, Unavailable.
            var similar = await _repo.GetSimilarRoomsAsync(6, "Quyết Thắng", 1400000m, limit: 3);

            // Same ward: 1 (|diff| 100k) then 3 (450k); other wards: 4 (400k) before 5 (600k) and 2 (800k).
            Assert.That(similar.Select(r => r.Id), Is.EqualTo(new[] { 1, 3, 4 }));
        }

        [Test]
        public async Task GetSimilarRoomsAsync_AC3_OnlyAvailableVerifiedAndNeverTheRoomItself()
        {
            var similar = await _repo.GetSimilarRoomsAsync(1, "Quyết Thắng", 1500000m, limit: 20);

            Assert.Multiple(() =>
            {
                Assert.That(similar.Any(r => r.Id == 1), Is.False, "The current room is excluded");
                Assert.That(similar.Any(r => r.Id is 6 or 7), Is.False, "Unavailable / unverified rooms are excluded");
                Assert.That(similar.All(r => r.IsVerified && r.Status == Constants.ListingStatuses.Available), Is.True);
                Assert.That(similar.Select(r => r.Id), Is.EquivalentTo(new[] { 2, 3, 4, 5 }));
            });
        }

        [Test]
        public async Task GetSimilarRoomsAsync_WardMatchIgnoresCaseAndSpaces()
        {
            var similar = await _repo.GetSimilarRoomsAsync(6, "  tân thịnh ", 2000000m, limit: 2);

            Assert.That(similar.Select(r => r.Ward), Is.All.EqualTo("Tân Thịnh"));
        }

        [TestCase(0, 1)]
        [TestCase(-3, 1)]
        [TestCase(1000, 5)] // clamped to 20, but only 5 other public rooms exist
        public async Task GetSimilarRoomsAsync_LimitIsClamped(int limit, int expectedCount)
        {
            var similar = await _repo.GetSimilarRoomsAsync(6, "Quyết Thắng", 1400000m, limit);

            Assert.That(similar, Has.Count.EqualTo(expectedCount));
        }
    }
}
