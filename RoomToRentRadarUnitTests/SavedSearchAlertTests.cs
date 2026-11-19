using Microsoft.Extensions.Logging.Abstractions;
using RoomToRentRadar;
using RoomToRentRadar.Data;
using RoomToRentRadar.Infrastructure.Persistence;
using RoomToRentRadar.Models;

namespace RoomToRentRadarUnitTests
{
    /// <summary>
    /// Comprehensive test suite for US08: Saved-Search Model & Alert Criteria API (Task T08.1).
    /// Tests SQLite-backed CRUD, criteria matching engine, and alert badge calculation.
    /// Acceptance Criteria:
    ///   AC1: In-app alerts can be enabled for any saved search criteria.
    ///   AC2: An alert badge is shown when a matching listing is published.
    ///   AC3: Users can pause or delete an alert at any time.
    /// </summary>
    [TestFixture]
    [CancelAfter(10000)]
    public class SavedSearchAlertTests
    {
        private string _tempDbPath = null!;
        private RentalDatabaseService _databaseService = null!;
        private SavedSearchAlertRepository _repository = null!;

        [SetUp]
        public async Task SetUp()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"rtr_alert_test_{Guid.NewGuid():N}.db3");
            var options = new DatabaseOptions($"Data Source={_tempDbPath}");
            _databaseService = new RentalDatabaseService(NullLogger<RentalDatabaseService>.Instance, options);
            await _databaseService.EnsureInitializedAsync();
            _repository = new SavedSearchAlertRepository(_databaseService);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_tempDbPath) && File.Exists(_tempDbPath))
            {
                try { File.Delete(_tempDbPath); } catch { }
            }
        }

        #region AC1: Saved Search Alert CRUD Tests

        [Test]
        public async Task SaveAlertAsync_NewAlert_InsertsAndReturnsPositiveId()
        {
            // AC1: In-app alerts can be enabled for any saved search criteria
            var alert = new SavedSearchAlert
            {
                UserId = 1,
                Name = "Tìm phòng trọ Z115 < 2 triệu",
                MinPrice = 1000000m,
                MaxPrice = 2000000m,
                MinArea = 15,
                MaxDistanceKm = 1.5,
                RoomType = "Phòng khép kín",
                IsEnabled = true
            };

            var alertId = await _repository.SaveAlertAsync(alert);

            Assert.That(alertId, Is.GreaterThan(0));
            Assert.That(alert.Id, Is.EqualTo(alertId));

            var saved = await _repository.GetByIdAsync(alertId);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.Name, Is.EqualTo("Tìm phòng trọ Z115 < 2 triệu"));
            Assert.That(saved.MinPrice, Is.EqualTo(1000000m));
            Assert.That(saved.MaxPrice, Is.EqualTo(2000000m));
            Assert.That(saved.MinArea, Is.EqualTo(15));
            Assert.That(saved.MaxDistanceKm, Is.EqualTo(1.5));
            Assert.That(saved.RoomType, Is.EqualTo("Phòng khép kín"));
            Assert.That(saved.IsEnabled, Is.True);
        }

        [Test]
        public async Task SaveAlertAsync_ExistingAlert_UpdatesCriteriaSuccessfully()
        {
            // AC1: Update existing alert criteria
            var alert = new SavedSearchAlert
            {
                UserId = 1,
                Name = "Tìm phòng cũ",
                MaxPrice = 1500000m,
                IsEnabled = true
            };

            var alertId = await _repository.SaveAlertAsync(alert);

            // Update fields
            alert.Name = "Tìm phòng mới cập nhật";
            alert.MaxPrice = 2500000m;
            alert.RoomType = "Chung cư mini";
            var updatedId = await _repository.SaveAlertAsync(alert);

            Assert.That(updatedId, Is.EqualTo(alertId));

            var updated = await _repository.GetByIdAsync(alertId);
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.Name, Is.EqualTo("Tìm phòng mới cập nhật"));
            Assert.That(updated.MaxPrice, Is.EqualTo(2500000m));
            Assert.That(updated.RoomType, Is.EqualTo("Chung cư mini"));
        }

        [Test]
        public async Task ListByUserAsync_ReturnsOnlyAlertsBelongingToRequestedUser()
        {
            // AC1: User-specific saved search alerts
            await _repository.SaveAlertAsync(new SavedSearchAlert { UserId = 1, Name = "User 1 Alert A" });
            await _repository.SaveAlertAsync(new SavedSearchAlert { UserId = 1, Name = "User 1 Alert B" });
            await _repository.SaveAlertAsync(new SavedSearchAlert { UserId = 2, Name = "User 2 Alert" });

            var user1Alerts = await _repository.ListByUserAsync(1);
            var user2Alerts = await _repository.ListByUserAsync(2);

            Assert.That(user1Alerts.Count, Is.EqualTo(2));
            Assert.That(user1Alerts.All(a => a.UserId == 1), Is.True);

            Assert.That(user2Alerts.Count, Is.EqualTo(1));
            Assert.That(user2Alerts[0].UserId, Is.EqualTo(2));
            Assert.That(user2Alerts[0].Name, Is.EqualTo("User 2 Alert"));
        }

        [Test]
        public async Task ListByUserAsync_OrdersByCreatedAtDescending()
        {
            var alert1 = new SavedSearchAlert { UserId = 1, Name = "Oldest", CreatedAt = DateTime.UtcNow.AddHours(-2).ToString("O") };
            var alert2 = new SavedSearchAlert { UserId = 1, Name = "Newest", CreatedAt = DateTime.UtcNow.ToString("O") };

            await _repository.SaveAlertAsync(alert1);
            await _repository.SaveAlertAsync(alert2);

            var alerts = await _repository.ListByUserAsync(1);

            Assert.That(alerts.Count, Is.EqualTo(2));
            Assert.That(alerts[0].Name, Is.EqualTo("Newest"));
            Assert.That(alerts[1].Name, Is.EqualTo("Oldest"));
        }

        [Test]
        public void SaveAlertAsync_NullAlert_ThrowsArgumentNullException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () => await _repository.SaveAlertAsync(null!));
        }

        [Test]
        public async Task SaveAlertAsync_EmptyName_SetsDefaultName()
        {
            var alert = new SavedSearchAlert
            {
                UserId = 1,
                Name = "",
                MaxPrice = 2000000m
            };

            var id = await _repository.SaveAlertAsync(alert);
            var saved = await _repository.GetByIdAsync(id);

            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.Name, Is.EqualTo("Tìm kiếm đã lưu"));
        }

        [Test]
        public async Task GetByIdAsync_NonExistentId_ReturnsNull()
        {
            var alert = await _repository.GetByIdAsync(99999);
            Assert.That(alert, Is.Null);
        }

        #endregion

        #region AC2: Criteria Matching Engine & Alert Badge Tests

        [Test]
        public void MatchesListing_MatchingPriceAndDistance_ReturnsTrue()
        {
            // AC2: An alert badge is shown when a matching listing is published
            var alert = new SavedSearchAlert
            {
                MinPrice = 1000000m,
                MaxPrice = 2000000m,
                MaxDistanceKm = 2.0,
                IsEnabled = true,
                AlertsSentToday = 0,
                MaxDailyAlerts = 3
            };

            var listing = new RoomListing
            {
                MonthlyRent = 1500000m,
                DistanceToCampusKm = 1.0,
                Status = Constants.ListingStatuses.Available,
                IsVerified = true
            };

            var isMatch = _repository.MatchesListing(alert, listing);
            Assert.That(isMatch, Is.True);
        }

        [Test]
        public void MatchesListing_MonthlyRentExceedsMaxPrice_ReturnsFalse()
        {
            var alert = new SavedSearchAlert
            {
                MaxPrice = 2000000m,
                IsEnabled = true
            };

            var listing = new RoomListing
            {
                MonthlyRent = 2500000m,
                Status = Constants.ListingStatuses.Available,
                IsVerified = true
            };

            var isMatch = _repository.MatchesListing(alert, listing);
            Assert.That(isMatch, Is.False);
        }

        [Test]
        public void MatchesListing_MonthlyRentBelowMinPrice_ReturnsFalse()
        {
            var alert = new SavedSearchAlert
            {
                MinPrice = 1800000m,
                IsEnabled = true
            };

            var listing = new RoomListing
            {
                MonthlyRent = 1200000m,
                Status = Constants.ListingStatuses.Available,
                IsVerified = true
            };

            var isMatch = _repository.MatchesListing(alert, listing);
            Assert.That(isMatch, Is.False);
        }

        [Test]
        public void MatchesListing_AreaBelowMinArea_ReturnsFalse()
        {
            var alert = new SavedSearchAlert
            {
                MinArea = 25.0,
                IsEnabled = true
            };

            var listing = new RoomListing
            {
                Area = 20.0,
                Status = Constants.ListingStatuses.Available,
                IsVerified = true
            };

            var isMatch = _repository.MatchesListing(alert, listing);
            Assert.That(isMatch, Is.False);
        }

        [Test]
        public void MatchesListing_DistanceExceedsMaxDistance_ReturnsFalse()
        {
            var alert = new SavedSearchAlert
            {
                MaxDistanceKm = 1.0,
                IsEnabled = true
            };

            var listing = new RoomListing
            {
                DistanceToCampusKm = 2.5,
                Status = Constants.ListingStatuses.Available,
                IsVerified = true
            };

            var isMatch = _repository.MatchesListing(alert, listing);
            Assert.That(isMatch, Is.False);
        }

        [Test]
        public void MatchesListing_RoomTypeMismatch_ReturnsFalse()
        {
            var alert = new SavedSearchAlert
            {
                RoomType = "Chung cư mini",
                IsEnabled = true
            };

            var listing = new RoomListing
            {
                RoomType = "Phòng trọ giá rẻ",
                Status = Constants.ListingStatuses.Available,
                IsVerified = true
            };

            var isMatch = _repository.MatchesListing(alert, listing);
            Assert.That(isMatch, Is.False);
        }

        [Test]
        public void MatchesListing_RoomTypeMatchesCaseInsensitive_ReturnsTrue()
        {
            var alert = new SavedSearchAlert
            {
                RoomType = "phòng khép kín",
                IsEnabled = true
            };

            var listing = new RoomListing
            {
                RoomType = "Phòng Khép Kín",
                Status = Constants.ListingStatuses.Available,
                IsVerified = true
            };

            var isMatch = _repository.MatchesListing(alert, listing);
            Assert.That(isMatch, Is.True);
        }

        [Test]
        public void MatchesListing_SpatialRadiusWithinRadius_ReturnsTrue()
        {
            // Point near ICTU campus
            var alert = new SavedSearchAlert
            {
                CenterLatitude = Constants.IctuLatitude,
                CenterLongitude = Constants.IctuLongitude,
                RadiusKm = 1.0,
                IsEnabled = true
            };

            // ~200 meters away
            var listing = new RoomListing
            {
                Latitude = Constants.IctuLatitude + 0.001,
                Longitude = Constants.IctuLongitude + 0.001,
                Status = Constants.ListingStatuses.Available,
                IsVerified = true
            };

            var isMatch = _repository.MatchesListing(alert, listing);
            Assert.That(isMatch, Is.True);
        }

        [Test]
        public void MatchesListing_SpatialRadiusOutsideRadius_ReturnsFalse()
        {
            var alert = new SavedSearchAlert
            {
                CenterLatitude = Constants.IctuLatitude,
                CenterLongitude = Constants.IctuLongitude,
                RadiusKm = 0.5,
                IsEnabled = true
            };

            // Far point (~5km away)
            var listing = new RoomListing
            {
                Latitude = Constants.IctuLatitude + 0.05,
                Longitude = Constants.IctuLongitude + 0.05,
                Status = Constants.ListingStatuses.Available,
                IsVerified = true
            };

            var isMatch = _repository.MatchesListing(alert, listing);
            Assert.That(isMatch, Is.False);
        }

        [Test]
        public void MatchesListing_DisabledAlert_ReturnsFalse()
        {
            var alert = new SavedSearchAlert
            {
                IsEnabled = false // Paused alert
            };

            var listing = new RoomListing
            {
                Status = Constants.ListingStatuses.Available,
                IsVerified = true
            };

            var isMatch = _repository.MatchesListing(alert, listing);
            Assert.That(isMatch, Is.False);
        }

        [Test]
        public void MatchesListing_AlertExceedsDailyLimit_ReturnsFalse()
        {
            var alert = new SavedSearchAlert
            {
                IsEnabled = true,
                AlertsSentToday = 3,
                MaxDailyAlerts = 3 // AC4/US08 anti-spam limit reached
            };

            var listing = new RoomListing
            {
                Status = Constants.ListingStatuses.Available,
                IsVerified = true
            };

            var isMatch = _repository.MatchesListing(alert, listing);
            Assert.That(isMatch, Is.False);
        }

        [Test]
        public async Task FindMatchingAlertsForListingAsync_ReturnsMatchingAlerts()
        {
            // Seed 2 alerts: 1 matches, 1 doesn't match
            await _repository.SaveAlertAsync(new SavedSearchAlert
            {
                UserId = 1,
                Name = "Khớp phòng",
                MaxPrice = 3000000m,
                MaxDistanceKm = 2.0,
                IsEnabled = true
            });

            await _repository.SaveAlertAsync(new SavedSearchAlert
            {
                UserId = 2,
                Name = "Không khớp vì giá quá rẻ",
                MaxPrice = 500000m,
                IsEnabled = true
            });

            var newListing = new RoomListing
            {
                MonthlyRent = 1500000m,
                DistanceToCampusKm = 1.0,
                Status = Constants.ListingStatuses.Available,
                IsVerified = true
            };

            var matches = await _repository.FindMatchingAlertsForListingAsync(newListing);

            Assert.That(matches, Is.Not.Null);
            Assert.That(matches.Count, Is.EqualTo(1));
            Assert.That(matches[0].Name, Is.EqualTo("Khớp phòng"));
        }

        [Test]
        public async Task GetMatchingListingCountAsync_ReturnsAccurateCountForAlertBadge()
        {
            // AC2: An alert badge is shown when a matching listing is published
            // Filter matching some of the 7 seeded ICTU rooms
            var alertId = await _repository.SaveAlertAsync(new SavedSearchAlert
            {
                UserId = 1,
                Name = "Badge test",
                MaxPrice = 2000000m,
                MaxDistanceKm = 1.5,
                IsEnabled = true
            });

            var badgeCount = await _repository.GetMatchingListingCountAsync(alertId);

            Assert.That(badgeCount, Is.GreaterThan(0), "Alert badge count must be greater than 0 for budget ICTU rooms.");
        }

        [Test]
        public async Task GetTotalMatchingBadgesForUserAsync_ReturnsAggregateBadgeCount()
        {
            // AC2: Bell badge count across all user alerts
            await _repository.SaveAlertAsync(new SavedSearchAlert
            {
                UserId = 10,
                Name = "Alert 1",
                MaxPrice = 2000000m,
                IsEnabled = true
            });

            await _repository.SaveAlertAsync(new SavedSearchAlert
            {
                UserId = 10,
                Name = "Alert 2",
                MaxPrice = 3000000m,
                IsEnabled = true
            });

            var totalBadges = await _repository.GetTotalMatchingBadgesForUserAsync(10);
            Assert.That(totalBadges, Is.GreaterThan(0));
        }

        [Test]
        public async Task IncrementAlertsSentCountAsync_IncrementsCounter()
        {
            var id = await _repository.SaveAlertAsync(new SavedSearchAlert
            {
                UserId = 1,
                Name = "Counter test",
                AlertsSentToday = 0
            });

            var success = await _repository.IncrementAlertsSentCountAsync(id);
            Assert.That(success, Is.True);

            var updated = await _repository.GetByIdAsync(id);
            Assert.That(updated!.AlertsSentToday, Is.EqualTo(1));
        }

        #endregion

        #region AC3: Pause & Delete Alert Tests

        [Test]
        public async Task ToggleAlertAsync_PauseAlert_SetsIsEnabledToFalse()
        {
            // AC3: Users can pause an alert at any time
            var alertId = await _repository.SaveAlertAsync(new SavedSearchAlert
            {
                UserId = 1,
                Name = "Pause test",
                IsEnabled = true
            });

            var success = await _repository.ToggleAlertAsync(alertId, false);
            Assert.That(success, Is.True);

            var paused = await _repository.GetByIdAsync(alertId);
            Assert.That(paused!.IsEnabled, Is.False);
        }

        [Test]
        public async Task ToggleAlertAsync_ResumeAlert_SetsIsEnabledToTrue()
        {
            // AC3: Users can resume a paused alert at any time
            var alertId = await _repository.SaveAlertAsync(new SavedSearchAlert
            {
                UserId = 1,
                Name = "Resume test",
                IsEnabled = false
            });

            var success = await _repository.ToggleAlertAsync(alertId, true);
            Assert.That(success, Is.True);

            var resumed = await _repository.GetByIdAsync(alertId);
            Assert.That(resumed!.IsEnabled, Is.True);
        }

        [Test]
        public async Task ToggleAlertAsync_NonExistentAlert_ReturnsFalse()
        {
            var success = await _repository.ToggleAlertAsync(99999, false);
            Assert.That(success, Is.False);
        }

        [Test]
        public async Task DeleteAlertAsync_DeletesAlertPermanently()
        {
            // AC3: Users can delete an alert at any time
            var alertId = await _repository.SaveAlertAsync(new SavedSearchAlert
            {
                UserId = 1,
                Name = "Delete test",
                IsEnabled = true
            });

            var deleteSuccess = await _repository.DeleteAlertAsync(alertId);
            Assert.That(deleteSuccess, Is.True);

            var deleted = await _repository.GetByIdAsync(alertId);
            Assert.That(deleted, Is.Null, "Deleted alert must not exist in SQLite database.");
        }

        [Test]
        public async Task DeleteAlertAsync_NonExistentAlert_ReturnsFalse()
        {
            var success = await _repository.DeleteAlertAsync(99999);
            Assert.That(success, Is.False);
        }

        #endregion
    }
}
