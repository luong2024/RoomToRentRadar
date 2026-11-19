using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using RoomToRentRadar;
using RoomToRentRadar.Data;
using RoomToRentRadar.Infrastructure.Persistence;
using RoomToRentRadar.Models;

namespace RoomToRentRadarUnitTests
{
    /// <summary>
    /// Comprehensive test suite for US05: Multi-Filter Search Service.
    /// Tests SQLite-backed queries, sorting, and edge cases with strict timeouts.
    /// </summary>
    [TestFixture]
    [CancelAfter(5000)]
    public class RoomListingSearchTests
    {
        private string _tempDbPath = null!;
        private RentalDatabaseService _databaseService = null!;
        private RoomListingRepository _repository = null!;

        [SetUp]
        public async Task SetUp()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"rtr_search_test_{Guid.NewGuid():N}.db3");
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
        public async Task SearchAsync_NullOrEmptyCriteria_ReturnsOnlyAvailableVerifiedRooms()
        {
            var results = await _repository.SearchAsync(new RoomFilterCriteria());

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.GreaterThan(0));
            Assert.That(results.All(r => r.IsVerified && r.Status == Constants.ListingStatuses.Available), Is.True);
        }

        [Test]
        public async Task SearchAsync_FilterByPriceRange_ReturnsOnlyRoomsWithinBudget()
        {
            var criteria = new RoomFilterCriteria
            {
                MinPrice = 1200000m,
                MaxPrice = 1800000m
            };

            var results = await _repository.SearchAsync(criteria);

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.GreaterThan(0));
            Assert.That(results.All(r => r.MonthlyRent >= 1200000m && r.MonthlyRent <= 1800000m), Is.True);
        }

        [Test]
        public async Task SearchAsync_FilterByArea_ReturnsOnlyRoomsMatchingAreaCriteria()
        {
            var criteria = new RoomFilterCriteria
            {
                MinArea = 20,
                MaxArea = 30
            };

            var results = await _repository.SearchAsync(criteria);

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.GreaterThan(0));
            Assert.That(results.All(r => r.Area >= 20 && r.Area <= 30), Is.True);
        }

        [Test]
        public async Task SearchAsync_FilterByRoomType_ReturnsCorrectType()
        {
            var criteria = new RoomFilterCriteria
            {
                RoomType = "Phòng khép kín"
            };

            var results = await _repository.SearchAsync(criteria);

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.GreaterThan(0));
            Assert.That(results.All(r => string.Equals(r.RoomType, "Phòng khép kín", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        [Test]
        public async Task SearchAsync_FilterByMaxDistance_ReturnsOnlyRoomsWithinRadius()
        {
            var criteria = new RoomFilterCriteria
            {
                MaxDistanceKm = 1.0
            };

            var results = await _repository.SearchAsync(criteria);

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.GreaterThan(0));
            Assert.That(results.All(r => r.DistanceToCampusKm <= 1.0), Is.True);
        }

        [Test]
        public async Task SearchAsync_FilterByKeyword_MatchesTitleOrDescriptionOrWard()
        {
            var criteria = new RoomFilterCriteria
            {
                Keyword = "Quyết Thắng"
            };

            var results = await _repository.SearchAsync(criteria);

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.GreaterThan(0));
            Assert.That(results.All(r =>
                r.Ward.Contains("Quyết Thắng", StringComparison.OrdinalIgnoreCase) ||
                r.Address.Contains("Quyết Thắng", StringComparison.OrdinalIgnoreCase) ||
                r.Title.Contains("Quyết Thắng", StringComparison.OrdinalIgnoreCase) ||
                r.Description.Contains("Quyết Thắng", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        [Test]
        public async Task SearchAsync_SortByPriceAsc_OrdersFromLowestToHighestPrice()
        {
            var criteria = new RoomFilterCriteria
            {
                SortBy = "PriceAsc"
            };

            var results = await _repository.SearchAsync(criteria);

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.GreaterThan(1));
            for (int i = 0; i < results.Count - 1; i++)
            {
                Assert.That(results[i].MonthlyRent, Is.LessThanOrEqualTo(results[i + 1].MonthlyRent));
            }
        }

        [Test]
        public async Task SearchAsync_SortByPriceDesc_OrdersFromHighestToLowestPrice()
        {
            var criteria = new RoomFilterCriteria
            {
                SortBy = "PriceDesc"
            };

            var results = await _repository.SearchAsync(criteria);

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.GreaterThan(1));
            for (int i = 0; i < results.Count - 1; i++)
            {
                Assert.That(results[i].MonthlyRent, Is.GreaterThanOrEqualTo(results[i + 1].MonthlyRent));
            }
        }

        [Test]
        public async Task SearchAsync_SortByDistanceAsc_OrdersFromClosestToFurthest()
        {
            var criteria = new RoomFilterCriteria
            {
                SortBy = "DistanceAsc"
            };

            var results = await _repository.SearchAsync(criteria);

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.GreaterThan(1));
            for (int i = 0; i < results.Count - 1; i++)
            {
                Assert.That(results[i].DistanceToCampusKm, Is.LessThanOrEqualTo(results[i + 1].DistanceToCampusKm));
            }
        }

        [Test]
        public async Task SearchAsync_CombinedMultiFilters_ReturnsAccurateIntersection()
        {
            var criteria = new RoomFilterCriteria
            {
                Keyword = "ICTU",
                MinPrice = 1000000m,
                MaxPrice = 3000000m,
                MaxDistanceKm = 2.0,
                SortBy = "PriceAsc"
            };

            var results = await _repository.SearchAsync(criteria);

            Assert.That(results, Is.Not.Null);
            foreach (var r in results)
            {
                Assert.That(r.MonthlyRent, Is.InRange(1000000m, 3000000m));
                Assert.That(r.DistanceToCampusKm, Is.LessThanOrEqualTo(2.0));
                Assert.That(r.IsVerified, Is.True);
                Assert.That(r.Status, Is.EqualTo(Constants.ListingStatuses.Available));
            }
        }

        [Test]
        public async Task SearchAsync_HighPerformanceBenchmark_CompletesWithinTwoSeconds()
        {
            var criteria = new RoomFilterCriteria
            {
                MinPrice = 500000m,
                MaxPrice = 5000000m,
                MaxDistanceKm = 5.0,
                SortBy = "DistanceAsc"
            };

            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 50; i++)
            {
                await _repository.SearchAsync(criteria);
            }
            stopwatch.Stop();

            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(2000), "50 search queries should complete in under 2 seconds.");
        }

        [Test]
        public async Task SearchAsync_AC2_SingleQueryLatency_Under1500Milliseconds()
        {
            // AC2: Results update within 1.5 seconds
            var criteria = new RoomFilterCriteria
            {
                Keyword = "ICTU",
                MinPrice = 1000000m,
                MaxPrice = 3000000m,
                MaxDistanceKm = 3.0,
                RoomType = "Phòng khép kín",
                SortBy = "DistanceAsc"
            };

            var stopwatch = Stopwatch.StartNew();
            var results = await _repository.SearchAsync(criteria);
            stopwatch.Stop();

            Assert.That(results, Is.Not.Null);
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1500), "Single multi-filter search must complete within 1.5 seconds (AC2).");
        }

        [Test]
        public async Task SearchAsync_AC3_ResultCount_ReturnsZeroWhenNoMatchesFound()
        {
            // AC3: Result count is always visible and handles zero results cleanly
            var criteria = new RoomFilterCriteria
            {
                MinPrice = 50000000m, // Unrealistic high price
                MaxPrice = 60000000m
            };

            var results = await _repository.SearchAsync(criteria);

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(0), "Result count must be 0 when no listings match the criteria.");
        }

        [Test]
        public void Criteria_AC3_ActiveFilterCount_CalculatesAccurately()
        {
            // AC3: Active filter count is always accurate and visible
            var criteria = new RoomFilterCriteria();
            Assert.That(criteria.ActiveFilterCount, Is.EqualTo(0));

            criteria.Keyword = "Phòng trọ";
            Assert.That(criteria.ActiveFilterCount, Is.EqualTo(1));

            criteria.MinPrice = 1000000m;
            Assert.That(criteria.ActiveFilterCount, Is.EqualTo(2));

            criteria.MaxArea = 25;
            Assert.That(criteria.ActiveFilterCount, Is.EqualTo(3));

            criteria.RoomType = "Chung cư mini";
            Assert.That(criteria.ActiveFilterCount, Is.EqualTo(4));

            criteria.MaxDistanceKm = 1.5;
            Assert.That(criteria.ActiveFilterCount, Is.EqualTo(5));

            criteria.Clear();
            Assert.That(criteria.ActiveFilterCount, Is.EqualTo(0));
        }
    }
}
