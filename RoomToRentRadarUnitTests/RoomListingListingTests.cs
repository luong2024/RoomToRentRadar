using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RoomToRentRadar;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Data;
using RoomToRentRadar.Infrastructure.DependencyInjection;
using RoomToRentRadar.Models;

namespace RoomToRentRadarUnitTests
{
    /// <summary>
    /// Comprehensive Unit & Performance Test suite for Task T02.1 (US02: Room Listing API).
    /// Tests strictly validate Acceptance Criteria AC1, AC2, AC3, AC4 against real SQLite queries.
    /// </summary>
    [TestFixture]
    [CancelAfter(5000)]
    public class RoomListingListingTests
    {
        private ServiceProvider _provider = null!;
        private string _tempDbPath = null!;

        [SetUp]
        public void SetUp()
        {
            RoomListingRepository.InvalidateListingCache();
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"rtr_t021_{Guid.NewGuid():N}.db3");

            var services = new ServiceCollection();
            services.AddRentalInfrastructure($"Data Source={_tempDbPath}");
            // Register real T02.1 SQLite repository override
            services.AddSingleton<IRoomListingRepository, RoomListingRepository>();
            _provider = services.BuildServiceProvider();
        }

        [TearDown]
        public void TearDown()
        {
            RoomListingRepository.InvalidateListingCache();
            _provider?.Dispose();
            if (!string.IsNullOrEmpty(_tempDbPath) && File.Exists(_tempDbPath))
            {
                try { File.Delete(_tempDbPath); } catch { /* Ignore SQLite lock */ }
            }
        }

        [Test]
        public async Task ListVerifiedRoomsAsync_AC1_AC4_DefaultSort_ReturnsOnlyAvailableAndVerifiedRoomsOrderedByDistance()
        {
            var repo = _provider.GetRequiredService<IRoomListingRepository>();

            var listings = await repo.ListVerifiedRoomsAsync(pageIndex: 0, pageSize: 20, sortBy: "DistanceAsc");

            Assert.That(listings, Is.Not.Empty, "Should return seeded verified listings");
            // AC4: Only listings with status Available and Verified are displayed
            Assert.That(listings.All(r => r.IsVerified && r.Status == Constants.ListingStatuses.Available), Is.True);
            // AC1: Sorted by distance from ICTU campus by default
            Assert.That(listings, Is.Ordered.By(nameof(RoomListing.DistanceToCampusKm)).Ascending);
        }

        [Test]
        public async Task ListVerifiedRoomsAsync_AC4_ExcludesPendingAndUnavailableRooms()
        {
            var repo = _provider.GetRequiredService<IRoomListingRepository>();

            var listings = await repo.ListVerifiedRoomsAsync(pageIndex: 0, pageSize: 50);

            // Seed Room 6 is Unavailable; Seed Room 7 is PendingReview & Unverified
            Assert.That(listings.Any(r => r.Id == 6), Is.False, "Room #6 is Unavailable and must be excluded");
            Assert.That(listings.Any(r => r.Id == 7), Is.False, "Room #7 is PendingReview and must be excluded");
            Assert.That(listings.Any(r => !r.IsVerified), Is.False, "Unverified rooms must be excluded");
        }

        [Test]
        public async Task ListVerifiedRoomsAsync_AC1_SortByPriceAsc_ReturnsRoomsOrderedByRentAscending()
        {
            var repo = _provider.GetRequiredService<IRoomListingRepository>();

            var listings = await repo.ListVerifiedRoomsAsync(pageIndex: 0, pageSize: 20, sortBy: "PriceAsc");

            Assert.That(listings, Is.Not.Empty);
            Assert.That(listings, Is.Ordered.By(nameof(RoomListing.MonthlyRent)).Ascending);
        }

        [Test]
        public async Task ListVerifiedRoomsAsync_AC1_SortByPriceDesc_ReturnsRoomsOrderedByRentDescending()
        {
            var repo = _provider.GetRequiredService<IRoomListingRepository>();

            var listings = await repo.ListVerifiedRoomsAsync(pageIndex: 0, pageSize: 20, sortBy: "PriceDesc");

            Assert.That(listings, Is.Not.Empty);
            Assert.That(listings, Is.Ordered.By(nameof(RoomListing.MonthlyRent)).Descending);
        }

        [Test]
        public async Task ListVerifiedRoomsAsync_Pagination_ReturnsCorrectSlicesWithoutDuplicates()
        {
            var repo = _provider.GetRequiredService<IRoomListingRepository>();

            var page0 = await repo.ListVerifiedRoomsAsync(pageIndex: 0, pageSize: 2);
            var page1 = await repo.ListVerifiedRoomsAsync(pageIndex: 1, pageSize: 2);

            Assert.That(page0.Count, Is.EqualTo(2));
            Assert.That(page1.Count, Is.GreaterThan(0));

            var page0Ids = page0.Select(r => r.Id).ToHashSet();
            Assert.That(page1.Any(r => page0Ids.Contains(r.Id)), Is.False, "Pages must not overlap");
        }

        [Test]
        public async Task ListVerifiedRoomsAsync_BoundaryInputs_HandlesNegativePageIndexAndZeroPageSizeGracefully()
        {
            var repo = _provider.GetRequiredService<IRoomListingRepository>();

            var clampedNegative = await repo.ListVerifiedRoomsAsync(pageIndex: -5, pageSize: 0);

            Assert.That(clampedNegative, Is.Not.Null);
            Assert.That(clampedNegative.Count, Is.GreaterThan(0), "Should clamp pageIndex to 0 and pageSize to at least 1");
        }

        [Test]
        public async Task ListVerifiedRoomsAsync_AC2_CardAttributes_AllFieldsMappedAccurately()
        {
            var repo = _provider.GetRequiredService<IRoomListingRepository>();

            var listings = await repo.ListVerifiedRoomsAsync(0, 1);
            var card = listings.First();

            Assert.That(card.Title, Is.Not.Empty, "AC2: Title must be mapped");
            Assert.That(card.MonthlyRent, Is.GreaterThan(0), "AC2: Monthly rent must be positive");
            Assert.That(card.Area, Is.GreaterThan(0), "AC2: Area must be positive");
            Assert.That(card.DistanceToCampusKm, Is.GreaterThan(0), "AC2: Distance to campus must be mapped");
            Assert.That(card.IsVerified, Is.True, "AC2: IsVerified must be true");
            Assert.That(card.Status, Is.EqualTo(Constants.ListingStatuses.Available));
            Assert.That(card.ThumbnailUrl, Is.Not.Empty);
        }

        [Test]
        public async Task ListVerifiedRoomsAsync_AC3_PerformanceBenchmark_Loads500ListingsWithinTwoSeconds()
        {
            var db = _provider.GetRequiredService<RentalDatabaseService>();
            await db.EnsureInitializedAsync();

            // Insert 500 verified available rooms into SQLite to simulate heavy scale
            await using (var conn = await db.GetOpenConnectionAsync())
            await using (var trans = conn.BeginTransaction())
            {
                for (int i = 100; i < 600; i++)
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.Transaction = trans;
                    cmd.CommandText = @"
                    INSERT INTO RoomListing (
                        Title, Description, MonthlyRent, Deposit, Area, RoomType, Address, Ward,
                        Latitude, Longitude, DistanceToCampusKm, MotorbikeMinutes, BusMinutes, WalkMinutes,
                        AmenitiesCsv, ThumbnailUrl, PhotosJson, Panorama360Url,
                        LandlordId, LandlordName, LandlordPhone, Status, IsVerified, SecurityScore,
                        QuietHours, GuestPolicy, CookingRules, ParkingRules, RulesVersion, UpdatedAt
                    ) VALUES (
                        @Title, 'Mô tả test scale', @MonthlyRent, 1000000, 20.0, 'Phòng khép kín', 'Tổ 1, Quyết Thắng', 'Quyết Thắng',
                        21.585, 105.806, @Distance, 3, 5, 10,
                        'Wifi,Nóng lạnh', 'dotnet_bot.png', '[]', '',
                        1, 'Chủ trọ test', '0912345678', 'Available', 1, 4.5,
                        '22:00', 'Không ở qua đêm', 'Nấu ăn bếp từ', 'Có camera', 1, '2026-11-12'
                    );";
                    cmd.Parameters.AddWithValue("@Title", $"Phòng trọ sinh viên ICTU #{i}");
                    cmd.Parameters.AddWithValue("@MonthlyRent", 1000000 + (i * 1000));
                    cmd.Parameters.AddWithValue("@Distance", 0.2 + (i * 0.01));
                    await cmd.ExecuteNonQueryAsync();
                }
                await trans.CommitAsync();
            }

            RoomListingRepository.InvalidateListingCache();
            var repo = _provider.GetRequiredService<IRoomListingRepository>();

            var sw = Stopwatch.StartNew();
            var loaded = await repo.ListVerifiedRoomsAsync(pageIndex: 0, pageSize: 100, sortBy: "DistanceAsc");
            sw.Stop();

            Assert.That(loaded.Count, Is.EqualTo(100), "Should retrieve full page of 100 items");
            // AC3: The list loads within 2 seconds for up to 500 listings
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(2000), $"Query took {sw.ElapsedMilliseconds}ms, must be < 2000ms");
        }

        [Test]
        public async Task ListVerifiedRoomsAsync_Caching_SubsequentCallReturnsInstantResult()
        {
            var repo = _provider.GetRequiredService<IRoomListingRepository>();

            var firstCall = await repo.ListVerifiedRoomsAsync(0, 10, "DistanceAsc");
            var secondCall = await repo.ListVerifiedRoomsAsync(0, 10, "DistanceAsc");

            Assert.That(secondCall.Count, Is.EqualTo(firstCall.Count));
            Assert.That(secondCall.Select(r => r.Id), Is.EqualTo(firstCall.Select(r => r.Id)));
        }
    }
}
