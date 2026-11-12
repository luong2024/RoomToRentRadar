using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Infrastructure.Persistence;
using RoomToRentRadar.Infrastructure.Persistence.Seed;

namespace RoomToRentRadar.Data
{
    /// <summary>
    /// Shared SQLite Database Schema and Seed Data initializer for Sprint 1 (US01 to US13).
    /// LOCKED BY @architect: Feature developers inject this service into their Repositories
    /// and call <see cref="GetOpenConnectionAsync"/> or <see cref="EnsureInitializedAsync"/> before running queries.
    /// Kept in <c>RoomToRentRadar.Data</c> namespace so existing contracts and task instructions remain 100% compatible.
    /// </summary>
    public class RentalDatabaseService
    {
        static RentalDatabaseService()
        {
            SQLitePCL.Batteries_V2.Init();
        }

        private bool _isInitialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private readonly ILogger<RentalDatabaseService> _logger;
        private readonly string _connectionString;

        public RentalDatabaseService(ILogger<RentalDatabaseService> logger, DatabaseOptions options)
            : this(logger, options.ConnectionString)
        {
        }

        public RentalDatabaseService(ILogger<RentalDatabaseService> logger, string connectionString)
        {
            _logger = logger;
            _connectionString = connectionString;
        }

        public string ConnectionString => _connectionString;

        public async Task<SqliteConnection> GetOpenConnectionAsync()
        {
            await EnsureInitializedAsync();
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }

        public async Task EnsureInitializedAsync()
        {
            if (_isInitialized)
                return;

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized)
                    return;

                await using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                await using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = DatabaseSchema.CreateTablesSql;
                    await cmd.ExecuteNonQueryAsync();
                }

                await SeedDefaultDataIfEmptyAsync(connection);
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing RentalDatabaseService SQLite schema");
                throw;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private static async Task SeedDefaultDataIfEmptyAsync(SqliteConnection connection)
        {
            // 1. Seed default users (Student, Landlord, Admin)
            await using (var countUserCmd = connection.CreateCommand())
            {
                countUserCmd.CommandText = "SELECT COUNT(1) FROM UserAccount;";
                var userCount = Convert.ToInt32(await countUserCmd.ExecuteScalarAsync());
                if (userCount == 0)
                {
                    foreach (var u in IctuSeedData.Users())
                    {
                        await using var insertUsersCmd = connection.CreateCommand();
                        insertUsersCmd.CommandText = @"
                        INSERT INTO UserAccount (FullName, Email, PasswordHash, Phone, Role, SelectedCampus, CreatedAt)
                        VALUES (@FullName, @Email, @PasswordHash, @Phone, @Role, @SelectedCampus, @CreatedAt);";
                        insertUsersCmd.Parameters.AddWithValue("@FullName", u.FullName);
                        insertUsersCmd.Parameters.AddWithValue("@Email", u.Email);
                        insertUsersCmd.Parameters.AddWithValue("@PasswordHash", u.PasswordHash);
                        insertUsersCmd.Parameters.AddWithValue("@Phone", u.Phone);
                        insertUsersCmd.Parameters.AddWithValue("@Role", u.Role);
                        insertUsersCmd.Parameters.AddWithValue("@SelectedCampus", u.SelectedCampus);
                        insertUsersCmd.Parameters.AddWithValue("@CreatedAt", u.CreatedAt);
                        await insertUsersCmd.ExecuteNonQueryAsync();
                    }
                }
            }

            // 2. Seed sample rooms across all 3 statuses (Available, Unavailable, PendingReview)
            await using (var countRoomCmd = connection.CreateCommand())
            {
                countRoomCmd.CommandText = "SELECT COUNT(1) FROM RoomListing;";
                var roomCount = Convert.ToInt32(await countRoomCmd.ExecuteScalarAsync());
                if (roomCount == 0)
                {
                    foreach (var r in IctuSeedData.Rooms())
                    {
                        await using var cmd = connection.CreateCommand();
                        cmd.CommandText = @"
                        INSERT INTO RoomListing (
                            Title, Description, MonthlyRent, Deposit, Area, RoomType, Address, Ward,
                            Latitude, Longitude, DistanceToCampusKm, MotorbikeMinutes, BusMinutes, WalkMinutes,
                            AmenitiesCsv, ThumbnailUrl, PhotosJson, Panorama360Url,
                            LandlordId, LandlordName, LandlordPhone, Status, IsVerified, SecurityScore,
                            QuietHours, GuestPolicy, CookingRules, ParkingRules, RulesVersion, UpdatedAt
                        ) VALUES (
                            @Title, @Description, @MonthlyRent, @Deposit, @Area, @RoomType, @Address, @Ward,
                            @Latitude, @Longitude, @DistanceToCampusKm, @MotorbikeMinutes, @BusMinutes, @WalkMinutes,
                            @AmenitiesCsv, @ThumbnailUrl, @PhotosJson, @Panorama360Url,
                            @LandlordId, @LandlordName, @LandlordPhone, @Status, @IsVerified, @SecurityScore,
                            @QuietHours, @GuestPolicy, @CookingRules, @ParkingRules, @RulesVersion, @UpdatedAt
                        );";

                        cmd.Parameters.AddWithValue("@Title", r.Title);
                        cmd.Parameters.AddWithValue("@Description", r.Description);
                        cmd.Parameters.AddWithValue("@MonthlyRent", (double)r.MonthlyRent);
                        cmd.Parameters.AddWithValue("@Deposit", (double)r.Deposit);
                        cmd.Parameters.AddWithValue("@Area", r.Area);
                        cmd.Parameters.AddWithValue("@RoomType", r.RoomType);
                        cmd.Parameters.AddWithValue("@Address", r.Address);
                        cmd.Parameters.AddWithValue("@Ward", r.Ward);
                        cmd.Parameters.AddWithValue("@Latitude", r.Latitude);
                        cmd.Parameters.AddWithValue("@Longitude", r.Longitude);
                        cmd.Parameters.AddWithValue("@DistanceToCampusKm", r.DistanceToCampusKm);
                        cmd.Parameters.AddWithValue("@MotorbikeMinutes", r.MotorbikeMinutes);
                        cmd.Parameters.AddWithValue("@BusMinutes", r.BusMinutes);
                        cmd.Parameters.AddWithValue("@WalkMinutes", r.WalkMinutes);
                        cmd.Parameters.AddWithValue("@AmenitiesCsv", r.AmenitiesCsv);
                        cmd.Parameters.AddWithValue("@ThumbnailUrl", r.ThumbnailUrl);
                        cmd.Parameters.AddWithValue("@PhotosJson", r.PhotosJson);
                        cmd.Parameters.AddWithValue("@Panorama360Url", r.Panorama360Url);
                        cmd.Parameters.AddWithValue("@LandlordId", r.LandlordId);
                        cmd.Parameters.AddWithValue("@LandlordName", r.LandlordName);
                        cmd.Parameters.AddWithValue("@LandlordPhone", r.LandlordPhone);
                        cmd.Parameters.AddWithValue("@Status", r.Status);
                        cmd.Parameters.AddWithValue("@IsVerified", r.IsVerified ? 1 : 0);
                        cmd.Parameters.AddWithValue("@SecurityScore", r.SecurityScore);
                        cmd.Parameters.AddWithValue("@QuietHours", r.QuietHours);
                        cmd.Parameters.AddWithValue("@GuestPolicy", r.GuestPolicy);
                        cmd.Parameters.AddWithValue("@CookingRules", r.CookingRules);
                        cmd.Parameters.AddWithValue("@ParkingRules", r.ParkingRules);
                        cmd.Parameters.AddWithValue("@RulesVersion", r.RulesVersion);
                        cmd.Parameters.AddWithValue("@UpdatedAt", r.UpdatedAt);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            // 3. Seed sample HouseRuleVersion history (US12)
            await using (var countRuleCmd = connection.CreateCommand())
            {
                countRuleCmd.CommandText = "SELECT COUNT(1) FROM HouseRuleVersion;";
                if (Convert.ToInt32(await countRuleCmd.ExecuteScalarAsync()) == 0)
                {
                    foreach (var hr in IctuSeedData.HouseRules())
                    {
                        await using var insertRuleCmd = connection.CreateCommand();
                        insertRuleCmd.CommandText = @"
                        INSERT INTO HouseRuleVersion (RoomId, VersionNumber, QuietHours, GuestPolicy, CookingRules, ParkingRules, UpdatedAt)
                        VALUES (@RoomId, @VersionNumber, @QuietHours, @GuestPolicy, @CookingRules, @ParkingRules, @UpdatedAt);";
                        insertRuleCmd.Parameters.AddWithValue("@RoomId", hr.RoomId);
                        insertRuleCmd.Parameters.AddWithValue("@VersionNumber", hr.VersionNumber);
                        insertRuleCmd.Parameters.AddWithValue("@QuietHours", hr.QuietHours);
                        insertRuleCmd.Parameters.AddWithValue("@GuestPolicy", hr.GuestPolicy);
                        insertRuleCmd.Parameters.AddWithValue("@CookingRules", hr.CookingRules);
                        insertRuleCmd.Parameters.AddWithValue("@ParkingRules", hr.ParkingRules);
                        insertRuleCmd.Parameters.AddWithValue("@UpdatedAt", hr.UpdatedAt);
                        await insertRuleCmd.ExecuteNonQueryAsync();
                    }
                }
            }

            // 4. Seed sample ListingReport entry for moderation queue testing (US13)
            await using (var countReportCmd = connection.CreateCommand())
            {
                countReportCmd.CommandText = "SELECT COUNT(1) FROM ListingReport;";
                if (Convert.ToInt32(await countReportCmd.ExecuteScalarAsync()) == 0)
                {
                    foreach (var rep in IctuSeedData.Reports())
                    {
                        await using var insertReportCmd = connection.CreateCommand();
                        insertReportCmd.CommandText = @"
                        INSERT INTO ListingReport (RoomId, ReporterUserId, Reason, Note, Status, CreatedAt)
                        VALUES (@RoomId, @ReporterUserId, @Reason, @Note, @Status, @CreatedAt);";
                        insertReportCmd.Parameters.AddWithValue("@RoomId", rep.RoomId);
                        insertReportCmd.Parameters.AddWithValue("@ReporterUserId", rep.ReporterUserId);
                        insertReportCmd.Parameters.AddWithValue("@Reason", rep.Reason);
                        insertReportCmd.Parameters.AddWithValue("@Note", rep.Note);
                        insertReportCmd.Parameters.AddWithValue("@Status", rep.Status);
                        insertReportCmd.Parameters.AddWithValue("@CreatedAt", rep.CreatedAt);
                        await insertReportCmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }
    }
}
