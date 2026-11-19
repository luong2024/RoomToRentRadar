using Microsoft.Data.Sqlite;
using RoomToRentRadar.Abstractions.Alerts;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Data
{
    /// <summary>
    /// TASK OWNER: T08.1 (US08 — Build saved-search model and alert criteria API).
    /// Provides SQLite persistence and criteria matching logic for saved searches and new listing alerts.
    /// Acceptance Criteria:
    ///   AC1: In-app alerts can be enabled for any saved search criteria.
    ///   AC2: An alert badge is shown when a matching listing is published.
    ///   AC3: Users can pause or delete an alert at any time.
    /// </summary>
    public class SavedSearchAlertRepository : ISavedSearchAlertRepository
    {
        protected readonly RentalDatabaseService Database;

        public SavedSearchAlertRepository(RentalDatabaseService database)
        {
            Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #region AC1 & AC3: CRUD Operations

        /// <summary>
        /// AC1: Lists all saved search alerts for a given user, ordered by creation date descending.
        /// </summary>
        public virtual async Task<List<SavedSearchAlert>> ListByUserAsync(int userId)
        {
            var results = new List<SavedSearchAlert>();
            await using var connection = await Database.GetOpenConnectionAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    Id, UserId, Name, MinPrice, MaxPrice, MinArea, MaxDistanceKm, RoomType,
                    CenterLatitude, CenterLongitude, RadiusKm, PolygonJson,
                    IsEnabled, AlertsSentToday, MaxDailyAlerts, CreatedAt
                FROM SavedSearchAlert 
                WHERE UserId = @UserId 
                ORDER BY CreatedAt DESC, Id DESC;";
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapAlert(reader));
            }
            return results;
        }

        /// <summary>
        /// Helper: Retrieves a single saved search alert by its primary key ID.
        /// </summary>
        public virtual async Task<SavedSearchAlert?> GetByIdAsync(int alertId)
        {
            await using var connection = await Database.GetOpenConnectionAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    Id, UserId, Name, MinPrice, MaxPrice, MinArea, MaxDistanceKm, RoomType,
                    CenterLatitude, CenterLongitude, RadiusKm, PolygonJson,
                    IsEnabled, AlertsSentToday, MaxDailyAlerts, CreatedAt
                FROM SavedSearchAlert 
                WHERE Id = @Id 
                LIMIT 1;";
            cmd.Parameters.AddWithValue("@Id", alertId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapAlert(reader);
            }
            return null;
        }

        /// <summary>
        /// AC1: Saves a search alert. Performs an INSERT if Id &lt;= 0, or an UPDATE if Id &gt; 0.
        /// </summary>
        public virtual async Task<int> SaveAlertAsync(SavedSearchAlert alert)
        {
            if (alert is null) throw new ArgumentNullException(nameof(alert));

            // Sanitize alert fields
            if (string.IsNullOrWhiteSpace(alert.Name))
            {
                alert.Name = "Tìm kiếm đã lưu";
            }
            if (alert.MaxDailyAlerts <= 0)
            {
                alert.MaxDailyAlerts = 3;
            }

            await using var connection = await Database.GetOpenConnectionAsync();
            await using var cmd = connection.CreateCommand();

            if (alert.Id <= 0)
            {
                cmd.CommandText = @"
                    INSERT INTO SavedSearchAlert (
                        UserId, Name, MinPrice, MaxPrice, MinArea, MaxDistanceKm, RoomType,
                        CenterLatitude, CenterLongitude, RadiusKm, PolygonJson,
                        IsEnabled, AlertsSentToday, MaxDailyAlerts, CreatedAt
                    ) VALUES (
                        @UserId, @Name, @MinPrice, @MaxPrice, @MinArea, @MaxDistanceKm, @RoomType,
                        @CenterLatitude, @CenterLongitude, @RadiusKm, @PolygonJson,
                        @IsEnabled, @AlertsSentToday, @MaxDailyAlerts, @CreatedAt
                    );
                    SELECT last_insert_rowid();";
            }
            else
            {
                cmd.CommandText = @"
                    UPDATE SavedSearchAlert SET
                        UserId = @UserId,
                        Name = @Name,
                        MinPrice = @MinPrice,
                        MaxPrice = @MaxPrice,
                        MinArea = @MinArea,
                        MaxDistanceKm = @MaxDistanceKm,
                        RoomType = @RoomType,
                        CenterLatitude = @CenterLatitude,
                        CenterLongitude = @CenterLongitude,
                        RadiusKm = @RadiusKm,
                        PolygonJson = @PolygonJson,
                        IsEnabled = @IsEnabled,
                        AlertsSentToday = @AlertsSentToday,
                        MaxDailyAlerts = @MaxDailyAlerts
                    WHERE Id = @Id;";
                cmd.Parameters.AddWithValue("@Id", alert.Id);
            }

            cmd.Parameters.AddWithValue("@UserId", alert.UserId);
            cmd.Parameters.AddWithValue("@Name", alert.Name.Trim());
            cmd.Parameters.AddWithValue("@MinPrice", (object?)alert.MinPrice ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MaxPrice", (object?)alert.MaxPrice ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MinArea", (object?)alert.MinArea ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MaxDistanceKm", (object?)alert.MaxDistanceKm ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RoomType", string.IsNullOrWhiteSpace(alert.RoomType) ? DBNull.Value : (object)alert.RoomType.Trim());
            cmd.Parameters.AddWithValue("@CenterLatitude", (object?)alert.CenterLatitude ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CenterLongitude", (object?)alert.CenterLongitude ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RadiusKm", (object?)alert.RadiusKm ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PolygonJson", string.IsNullOrWhiteSpace(alert.PolygonJson) ? "[]" : alert.PolygonJson);
            cmd.Parameters.AddWithValue("@IsEnabled", alert.IsEnabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@AlertsSentToday", alert.AlertsSentToday);
            cmd.Parameters.AddWithValue("@MaxDailyAlerts", alert.MaxDailyAlerts);

            if (alert.Id <= 0)
            {
                cmd.Parameters.AddWithValue("@CreatedAt", string.IsNullOrWhiteSpace(alert.CreatedAt)
                    ? DateTime.UtcNow.ToString("O")
                    : alert.CreatedAt);

                var scalar = await cmd.ExecuteScalarAsync();
                var newId = Convert.ToInt32(scalar);
                alert.Id = newId;
                return newId;
            }

            await cmd.ExecuteNonQueryAsync();
            return alert.Id;
        }

        /// <summary>
        /// AC3: Pauses (isEnabled = false) or resumes (isEnabled = true) an alert at any time.
        /// </summary>
        public virtual async Task<bool> ToggleAlertAsync(int alertId, bool isEnabled)
        {
            await using var connection = await Database.GetOpenConnectionAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE SavedSearchAlert SET IsEnabled = @IsEnabled WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@IsEnabled", isEnabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@Id", alertId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        /// <summary>
        /// AC3: Deletes an alert permanently from SQLite at any time.
        /// </summary>
        public virtual async Task<bool> DeleteAlertAsync(int alertId)
        {
            await using var connection = await Database.GetOpenConnectionAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM SavedSearchAlert WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@Id", alertId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        #endregion

        #region AC2: Criteria Matching Engine & Alert Badge APIs

        /// <summary>
        /// AC2: Determines whether a newly published listing matches a saved search alert's criteria.
        /// Checks price range, area, room type, distance to campus, and spatial radius (Haversine).
        /// </summary>
        public virtual bool MatchesListing(SavedSearchAlert alert, RoomListing listing)
        {
            if (alert is null || listing is null) return false;

            // Only active alerts that haven't exceeded daily rate limit are matched
            if (!alert.IsEnabled) return false;
            if (alert.AlertsSentToday >= alert.MaxDailyAlerts) return false;

            // Must be available and verified
            if (!listing.IsVerified || !string.Equals(listing.Status, Constants.ListingStatuses.Available, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Price filter
            if (alert.MinPrice.HasValue && listing.MonthlyRent < alert.MinPrice.Value)
            {
                return false;
            }
            if (alert.MaxPrice.HasValue && listing.MonthlyRent > alert.MaxPrice.Value)
            {
                return false;
            }

            // Area filter
            if (alert.MinArea.HasValue && listing.Area < alert.MinArea.Value)
            {
                return false;
            }

            // Distance to campus filter
            if (alert.MaxDistanceKm.HasValue && listing.DistanceToCampusKm > alert.MaxDistanceKm.Value)
            {
                return false;
            }

            // Room type filter (case-insensitive, ignoring "Tất cả")
            if (!string.IsNullOrWhiteSpace(alert.RoomType) && !string.Equals(alert.RoomType, "Tất cả", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(alert.RoomType.Trim(), listing.RoomType?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Spatial radius filter (CenterLatitude, CenterLongitude, RadiusKm)
            if (alert.CenterLatitude.HasValue && alert.CenterLongitude.HasValue && alert.RadiusKm.HasValue && alert.RadiusKm.Value > 0)
            {
                var distance = CalculateDistanceKm(
                    alert.CenterLatitude.Value, alert.CenterLongitude.Value,
                    listing.Latitude, listing.Longitude);

                if (distance > alert.RadiusKm.Value)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// AC2: Finds all active student alerts that match a newly published room listing.
        /// Used by the system to trigger notifications and calculate badge updates.
        /// </summary>
        public virtual async Task<List<SavedSearchAlert>> FindMatchingAlertsForListingAsync(RoomListing listing)
        {
            var matchingAlerts = new List<SavedSearchAlert>();
            if (listing is null) return matchingAlerts;

            await using var connection = await Database.GetOpenConnectionAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    Id, UserId, Name, MinPrice, MaxPrice, MinArea, MaxDistanceKm, RoomType,
                    CenterLatitude, CenterLongitude, RadiusKm, PolygonJson,
                    IsEnabled, AlertsSentToday, MaxDailyAlerts, CreatedAt
                FROM SavedSearchAlert 
                WHERE IsEnabled = 1 AND AlertsSentToday < MaxDailyAlerts;";

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var alert = MapAlert(reader);
                if (MatchesListing(alert, listing))
                {
                    matchingAlerts.Add(alert);
                }
            }
            return matchingAlerts;
        }

        /// <summary>
        /// AC2: Returns the count of active listings matching a specific saved search alert.
        /// Powers the Alert Badge count displayed on UI items.
        /// </summary>
        public virtual async Task<int> GetMatchingListingCountAsync(int alertId)
        {
            var alert = await GetByIdAsync(alertId);
            if (alert is null || !alert.IsEnabled) return 0;

            await using var connection = await Database.GetOpenConnectionAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    Id, MonthlyRent, Area, RoomType, DistanceToCampusKm, Latitude, Longitude, Status, IsVerified
                FROM RoomListing 
                WHERE Status = @Status AND IsVerified = 1;";
            cmd.Parameters.AddWithValue("@Status", Constants.ListingStatuses.Available);

            int matchCount = 0;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var listing = new RoomListing
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    MonthlyRent = Convert.ToDecimal(reader["MonthlyRent"]),
                    Area = Convert.ToDouble(reader["Area"]),
                    RoomType = Convert.ToString(reader["RoomType"]) ?? string.Empty,
                    DistanceToCampusKm = Convert.ToDouble(reader["DistanceToCampusKm"]),
                    Latitude = Convert.ToDouble(reader["Latitude"]),
                    Longitude = Convert.ToDouble(reader["Longitude"]),
                    Status = Convert.ToString(reader["Status"]) ?? Constants.ListingStatuses.Available,
                    IsVerified = Convert.ToInt32(reader["IsVerified"]) != 0
                };

                if (MatchesListing(alert, listing))
                {
                    matchCount++;
                }
            }

            return matchCount;
        }

        /// <summary>
        /// AC2: Returns the total alert badge count for a user across all their active saved searches.
        /// Powers the top-level notification badge counter.
        /// </summary>
        public virtual async Task<int> GetTotalMatchingBadgesForUserAsync(int userId)
        {
            var alerts = await ListByUserAsync(userId);
            var enabledAlerts = alerts.Where(a => a.IsEnabled).ToList();
            if (enabledAlerts.Count == 0) return 0;

            int totalBadge = 0;
            foreach (var alert in enabledAlerts)
            {
                totalBadge += await GetMatchingListingCountAsync(alert.Id);
            }
            return totalBadge;
        }

        /// <summary>
        /// Increments the daily sent alert counter for rate limiting (MaxDailyAlerts).
        /// </summary>
        public virtual async Task<bool> IncrementAlertsSentCountAsync(int alertId)
        {
            await using var connection = await Database.GetOpenConnectionAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE SavedSearchAlert SET AlertsSentToday = AlertsSentToday + 1 WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@Id", alertId);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Calculates great-circle distance between two coordinates in kilometers using Haversine formula.
        /// </summary>
        protected static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusKm = 6371.0;
            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusKm * c;
        }

        /// <summary>
        /// Maps an SQLite reader row to a <see cref="SavedSearchAlert"/> entity.
        /// </summary>
        protected static SavedSearchAlert MapAlert(SqliteDataReader reader) => new()
        {
            Id = Convert.ToInt32(reader["Id"]),
            UserId = Convert.ToInt32(reader["UserId"]),
            Name = Convert.ToString(reader["Name"]) ?? string.Empty,
            MinPrice = reader["MinPrice"] is DBNull ? null : Convert.ToDecimal(reader["MinPrice"]),
            MaxPrice = reader["MaxPrice"] is DBNull ? null : Convert.ToDecimal(reader["MaxPrice"]),
            MinArea = reader["MinArea"] is DBNull ? null : Convert.ToDouble(reader["MinArea"]),
            MaxDistanceKm = reader["MaxDistanceKm"] is DBNull ? null : Convert.ToDouble(reader["MaxDistanceKm"]),
            RoomType = reader["RoomType"] is DBNull ? null : Convert.ToString(reader["RoomType"]),
            CenterLatitude = reader["CenterLatitude"] is DBNull ? null : Convert.ToDouble(reader["CenterLatitude"]),
            CenterLongitude = reader["CenterLongitude"] is DBNull ? null : Convert.ToDouble(reader["CenterLongitude"]),
            RadiusKm = reader["RadiusKm"] is DBNull ? null : Convert.ToDouble(reader["RadiusKm"]),
            PolygonJson = Convert.ToString(reader["PolygonJson"]) ?? "[]",
            IsEnabled = Convert.ToInt32(reader["IsEnabled"]) != 0,
            AlertsSentToday = Convert.ToInt32(reader["AlertsSentToday"]),
            MaxDailyAlerts = Convert.ToInt32(reader["MaxDailyAlerts"]),
            CreatedAt = Convert.ToString(reader["CreatedAt"]) ?? string.Empty
        };

        #endregion
    }
}
