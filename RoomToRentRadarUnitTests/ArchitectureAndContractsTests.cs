using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RoomToRentRadar;
using RoomToRentRadar.Abstractions.Alerts;
using RoomToRentRadar.Abstractions.Auth;
using RoomToRentRadar.Abstractions.Favorites;
using RoomToRentRadar.Abstractions.HouseRules;
using RoomToRentRadar.Abstractions.Reports;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Data;
using RoomToRentRadar.Infrastructure.DependencyInjection;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;

namespace RoomToRentRadarUnitTests
{
    /// <summary>
    /// Comprehensive Contract, API/Service Integration, and Concurrency/Timeout test suite for Group 5 Sprint 1.
    /// Every test enforces a strict 5-second timeout ([CancelAfter(5000)]) to catch async deadlocks immediately.
    /// When a backend developer registers their real SQLite service in DI, these contract tests can validate it directly.
    /// </summary>
    [CancelAfter(5000)]
    public class ArchitectureAndContractsTests
    {
        private ServiceProvider _provider = null!;
        private string _tempDbPath = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"rtr_contract_{Guid.NewGuid():N}.db3");
            var services = new ServiceCollection();
            services.AddRentalInfrastructure($"Data Source={_tempDbPath}");
            _provider = services.BuildServiceProvider();
        }

        [TearDown]
        public void TearDown()
        {
            _provider?.Dispose();
            if (!string.IsNullOrEmpty(_tempDbPath) && File.Exists(_tempDbPath))
            {
                try { File.Delete(_tempDbPath); } catch { /* ignore SQLite pool file lock on cleanup */ }
            }
        }

        [Test]
        public async Task RentalDatabaseService_InitializesSchema_SeedsIctuData_AndSupportsConcurrentAccess()
        {
            var db = _provider.GetRequiredService<RentalDatabaseService>();

            // Simulate 5 concurrent callers hitting EnsureInitializedAsync() at the same time (verifies SemaphoreSlim lock)
            await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => db.EnsureInitializedAsync()));

            await using var conn = await db.GetOpenConnectionAsync();
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT COUNT(1) FROM RoomListing;";
            Assert.That(Convert.ToInt32(await cmd.ExecuteScalarAsync()), Is.EqualTo(7), "Should seed 7 default ICTU rooms exactly once");

            cmd.CommandText = "SELECT COUNT(1) FROM UserAccount;";
            Assert.That(Convert.ToInt32(await cmd.ExecuteScalarAsync()), Is.EqualTo(3), "Should seed 3 default user accounts");

            cmd.CommandText = "SELECT COUNT(1) FROM HouseRuleVersion;";
            Assert.That(Convert.ToInt32(await cmd.ExecuteScalarAsync()), Is.EqualTo(2), "Should seed 2 house rule versions");

            cmd.CommandText = "SELECT COUNT(1) FROM ListingReport;";
            Assert.That(Convert.ToInt32(await cmd.ExecuteScalarAsync()), Is.EqualTo(1), "Should seed 1 listing report");
        }

        [Test]
        public async Task AuthContract_Login_Register_Logout_BehavesAccordingToSpec()
        {
            var auth = _provider.GetRequiredService<IAuthService>();
            Assert.That(auth.IsSignedIn, Is.False);

            // 1. Invalid login fails safely
            var failed = await auth.LoginAsync("student@ictu.edu.vn", "wrong-pass");
            Assert.That(failed, Is.Null);
            Assert.That(auth.IsSignedIn, Is.False);

            // 2. Seeded ICTU student login succeeds
            var student = await auth.LoginAsync("student@ictu.edu.vn", "123456");
            Assert.That(student, Is.Not.Null);
            Assert.That(auth.IsSignedIn, Is.True);
            Assert.That(auth.CurrentUser?.Role, Is.EqualTo(Constants.UserRoles.Student));

            // 3. Duplicate email registration is rejected
            var duplicate = await auth.RegisterAsync("Dup", "student@ictu.edu.vn", "123456", "0900000000", Constants.UserRoles.Student);
            Assert.That(duplicate, Is.Null);

            // 4. New user registration signs in automatically
            var newUser = await auth.RegisterAsync("Nguyễn Văn A", "newstudent@ictu.edu.vn", "abcdef", "0911222333", Constants.UserRoles.Student);
            Assert.That(newUser, Is.Not.Null);
            Assert.That(auth.CurrentUser?.Email, Is.EqualTo("newstudent@ictu.edu.vn"));

            // 5. Logout clears session
            auth.Logout();
            Assert.That(auth.IsSignedIn, Is.False);
            Assert.That(auth.CurrentUser, Is.Null);
        }

        [Test]
        public async Task RoomListingContract_List_Detail_Similar_Search_AndPublish_BehavesAccordingToSpec()
        {
            var repo = _provider.GetRequiredService<IRoomListingRepository>();

            // US02: Only Available + IsVerified rooms are returned, sorted by DistanceAsc by default
            var verified = await repo.ListVerifiedRoomsAsync(pageIndex: 0, pageSize: 20, sortBy: "DistanceAsc");
            Assert.That(verified, Is.Not.Empty);
            Assert.That(verified.All(r => r.IsVerified && r.Status == Constants.ListingStatuses.Available), Is.True);
            Assert.That(verified, Is.Ordered.By(nameof(RoomListing.DistanceToCampusKm)).Ascending);

            // US02: Sorting by PriceAsc & PriceDesc
            var byPriceAsc = await repo.ListVerifiedRoomsAsync(0, 20, "PriceAsc");
            Assert.That(byPriceAsc, Is.Ordered.By(nameof(RoomListing.MonthlyRent)).Ascending);

            var byPriceDesc = await repo.ListVerifiedRoomsAsync(0, 20, "PriceDesc");
            Assert.That(byPriceDesc, Is.Ordered.By(nameof(RoomListing.MonthlyRent)).Descending);

            // US03: GetById & Similar rooms when viewing an Unavailable room (Room #6)
            var unavailableRoom = await repo.GetByIdAsync(6);
            Assert.That(unavailableRoom, Is.Not.Null);
            Assert.That(unavailableRoom!.Status, Is.EqualTo(Constants.ListingStatuses.Unavailable));

            var similar = await repo.GetSimilarRoomsAsync(excludeRoomId: 6, ward: unavailableRoom.Ward, targetRent: unavailableRoom.MonthlyRent, limit: 3);
            Assert.That(similar.Count, Is.LessThanOrEqualTo(3));
            Assert.That(similar.Any(r => r.Id == 6), Is.False);
            Assert.That(similar.All(r => r.Status == Constants.ListingStatuses.Available && r.IsVerified), Is.True);

            // US05: Multi-filter search
            var criteria = new RoomFilterCriteria
            {
                MaxPrice = 1600000m,
                MaxDistanceKm = 1.0,
                RoomType = "Phòng khép kín"
            };
            Assert.That(criteria.ActiveFilterCount, Is.EqualTo(3));
            var filtered = await repo.SearchAsync(criteria);
            Assert.That(filtered.All(r =>
                r.MonthlyRent <= 1600000m &&
                r.DistanceToCampusKm <= 1.0 &&
                r.RoomType == "Phòng khép kín"), Is.True);

            // US09 AC2: Landlord publishing sets PendingReview & IsVerified = false
            var newListing = new RoomListing
            {
                Title = "Phòng test mới đăng gần cổng ICTU",
                MonthlyRent = 1400000m,
                Area = 20,
                Ward = "Quyết Thắng"
            };
            var newId = await repo.SaveListingAsync(newListing);
            Assert.That(newId, Is.GreaterThan(0));

            var saved = await repo.GetByIdAsync(newId);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.Status, Is.EqualTo(Constants.ListingStatuses.PendingReview));
            Assert.That(saved.IsVerified, Is.False);
        }

        [Test]
        public async Task Favorite_Alert_HouseRule_AndReport_Contracts_BehaveAccordingToSpec()
        {
            var favRepo = _provider.GetRequiredService<IFavoriteRepository>();
            var alertRepo = _provider.GetRequiredService<ISavedSearchAlertRepository>();
            var ruleRepo = _provider.GetRequiredService<IHouseRuleRepository>();
            var reportRepo = _provider.GetRequiredService<IListingReportRepository>();

            // US07: Favorites toggle & list
            var initialFavs = await favRepo.GetFavoriteRoomsAsync(userId: 1);
            var toggledOn = await favRepo.ToggleFavoriteAsync(userId: 1, roomId: 3);
            Assert.That(toggledOn, Is.True);
            var afterAdd = await favRepo.GetFavoriteRoomsAsync(userId: 1);
            Assert.That(afterAdd.Count, Is.EqualTo(initialFavs.Count + 1));

            // US08: Saved search alerts CRUD
            var alertId = await alertRepo.SaveAlertAsync(new SavedSearchAlert
            {
                UserId = 1,
                Name = "Alert < 1.5tr",
                MaxPrice = 1500000m
            });
            Assert.That(alertId, Is.GreaterThan(0));
            Assert.That(await alertRepo.ToggleAlertAsync(alertId, false), Is.True);
            Assert.That(await alertRepo.DeleteAlertAsync(alertId), Is.True);

            // US12: House rule versioning increments VersionNumber
            var historyBefore = await ruleRepo.GetHistoryByRoomAsync(roomId: 1);
            await ruleRepo.SaveNewVersionAsync(new HouseRuleVersion
            {
                RoomId = 1,
                QuietHours = "23:00 - 05:30",
                GuestPolicy = "Không ở qua đêm"
            });
            var historyAfter = await ruleRepo.GetHistoryByRoomAsync(roomId: 1);
            Assert.That(historyAfter.Count, Is.EqualTo(historyBefore.Count + 1));
            Assert.That(historyAfter.First().VersionNumber, Is.GreaterThan(historyBefore.First().VersionNumber));

            // US13: Submit & list pending reports
            var pendingBefore = await reportRepo.ListPendingReportsAsync();
            var repId = await reportRepo.SubmitReportAsync(new ListingReport
            {
                RoomId = 2,
                ReporterUserId = 1,
                Reason = "Sai giá thuê",
                Note = "Giá thực tế cao hơn tin đăng"
            });
            Assert.That(repId, Is.GreaterThan(0));
            var pendingAfter = await reportRepo.ListPendingReportsAsync();
            Assert.That(pendingAfter.Count, Is.EqualTo(pendingBefore.Count + 1));
        }

        [Test]
        public void ComparisonBasketService_EnforcesMaxThreeUniqueRooms()
        {
            var basket = new ComparisonBasketService();

            Assert.That(basket.TryAddRoom(new RoomListing { Id = 1, Title = "R1" }, out _), Is.True);
            Assert.That(basket.TryAddRoom(new RoomListing { Id = 1, Title = "R1 dup" }, out _), Is.False);
            Assert.That(basket.TryAddRoom(new RoomListing { Id = 2, Title = "R2" }, out _), Is.True);
            Assert.That(basket.TryAddRoom(new RoomListing { Id = 3, Title = "R3" }, out _), Is.True);
            Assert.That(basket.TryAddRoom(new RoomListing { Id = 4, Title = "R4" }, out _), Is.False);
            Assert.That(basket.SelectedRooms.Count, Is.EqualTo(ComparisonBasketService.MaxComparisonRooms));
        }
    }
}
