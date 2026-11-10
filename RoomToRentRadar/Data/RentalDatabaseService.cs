using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Data
{
    /// <summary>
    /// Shared SQLite Database Schema and Seed Data initializer for Sprint 1 (US01 to US13).
    /// LOCKED BY @architect: Feature developers should inject this service into their Repositories
    /// and call EnsureInitializedAsync() before running queries.
    /// </summary>
    public class RentalDatabaseService
    {
        private bool _isInitialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private readonly ILogger<RentalDatabaseService> _logger;
        private readonly string _connectionString;

        public RentalDatabaseService(ILogger<RentalDatabaseService> logger)
            : this(logger, Constants.DatabasePath)
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

                var ddl = @"
                CREATE TABLE IF NOT EXISTS UserAccount (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NOT NULL,
                    Email TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    Phone TEXT NOT NULL,
                    Role TEXT NOT NULL,
                    SelectedCampus TEXT NOT NULL DEFAULT 'ICTU',
                    CreatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS RoomListing (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    MonthlyRent REAL NOT NULL,
                    Deposit REAL NOT NULL,
                    Area REAL NOT NULL,
                    RoomType TEXT NOT NULL,
                    Address TEXT NOT NULL,
                    Ward TEXT NOT NULL,
                    Latitude REAL NOT NULL,
                    Longitude REAL NOT NULL,
                    DistanceToCampusKm REAL NOT NULL,
                    MotorbikeMinutes INTEGER NOT NULL,
                    BusMinutes INTEGER NOT NULL,
                    WalkMinutes INTEGER NOT NULL,
                    AmenitiesCsv TEXT NOT NULL,
                    ThumbnailUrl TEXT NOT NULL,
                    PhotosJson TEXT NOT NULL,
                    Panorama360Url TEXT NOT NULL,
                    LandlordId INTEGER NOT NULL,
                    LandlordName TEXT NOT NULL,
                    LandlordPhone TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    IsVerified INTEGER NOT NULL,
                    SecurityScore REAL NOT NULL,
                    QuietHours TEXT NOT NULL,
                    GuestPolicy TEXT NOT NULL,
                    CookingRules TEXT NOT NULL,
                    ParkingRules TEXT NOT NULL,
                    RulesVersion INTEGER NOT NULL DEFAULT 1,
                    UpdatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS FavoriteRoom (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    RoomId INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS SavedSearchAlert (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    MinPrice REAL NULL,
                    MaxPrice REAL NULL,
                    MinArea REAL NULL,
                    MaxDistanceKm REAL NULL,
                    RoomType TEXT NULL,
                    CenterLatitude REAL NULL,
                    CenterLongitude REAL NULL,
                    RadiusKm REAL NULL,
                    PolygonJson TEXT NOT NULL DEFAULT '[]',
                    IsEnabled INTEGER NOT NULL DEFAULT 1,
                    AlertsSentToday INTEGER NOT NULL DEFAULT 0,
                    MaxDailyAlerts INTEGER NOT NULL DEFAULT 3,
                    CreatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS HouseRuleVersion (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RoomId INTEGER NOT NULL,
                    VersionNumber INTEGER NOT NULL,
                    QuietHours TEXT NOT NULL,
                    GuestPolicy TEXT NOT NULL,
                    CookingRules TEXT NOT NULL,
                    ParkingRules TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ListingReport (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RoomId INTEGER NOT NULL,
                    ReporterUserId INTEGER NOT NULL,
                    Reason TEXT NOT NULL,
                    Note TEXT NOT NULL,
                    Status TEXT NOT NULL DEFAULT 'Pending',
                    CreatedAt TEXT NOT NULL
                );";

                await using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = ddl;
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
                    var insertUsersCmd = connection.CreateCommand();
                    insertUsersCmd.CommandText = @"
                    INSERT INTO UserAccount (FullName, Email, PasswordHash, Phone, Role, SelectedCampus, CreatedAt) VALUES
                    ('Sinh viên ICTU (Demo)', 'student@ictu.edu.vn', '123456', '0988111222', 'Student', 'ICTU', '2026-11-09'),
                    ('Chú Hòa Chủ Trọ Z115', 'landlord@ictu.edu.vn', '123456', '0912333444', 'Landlord', 'ICTU', '2026-11-09'),
                    ('Ban Quản Lý Nội Trú ICTU', 'admin@ictu.edu.vn', '123456', '0909555666', 'Admin', 'ICTU', '2026-11-09');";
                    await insertUsersCmd.ExecuteNonQueryAsync();
                }
            }

            // 2. Seed sample rooms across all 3 statuses (Available, Unavailable, PendingReview)
            await using (var countRoomCmd = connection.CreateCommand())
            {
                countRoomCmd.CommandText = "SELECT COUNT(1) FROM RoomListing;";
                var roomCount = Convert.ToInt32(await countRoomCmd.ExecuteScalarAsync());
                if (roomCount == 0)
                {
                    var sampleRooms = GetDefaultIctuRooms();
                    foreach (var r in sampleRooms)
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
                    await using var insertRuleCmd = connection.CreateCommand();
                    insertRuleCmd.CommandText = @"
                    INSERT INTO HouseRuleVersion (RoomId, VersionNumber, QuietHours, GuestPolicy, CookingRules, ParkingRules, UpdatedAt) VALUES
                    (1, 1, '22:00 - 05:30', 'Không tiếp khách sau 22:00', 'Được nấu ăn bằng bếp điện', 'Mỗi phòng 1 xe máy', '2026-10-01 08:00'),
                    (1, 2, '22:30 - 05:30', 'Khách đến chơi báo trước, không ở qua đêm sau 22:30', 'Được nấu ăn bằng bếp từ hoặc bếp hồng ngoại', 'Mỗi phòng tối đa 2 xe máy tại nhà xe có camera', '2026-11-09 08:30');";
                    await insertRuleCmd.ExecuteNonQueryAsync();
                }
            }

            // 4. Seed sample ListingReport entry for moderation queue testing (US13)
            await using (var countReportCmd = connection.CreateCommand())
            {
                countReportCmd.CommandText = "SELECT COUNT(1) FROM ListingReport;";
                if (Convert.ToInt32(await countReportCmd.ExecuteScalarAsync()) == 0)
                {
                    await using var insertReportCmd = connection.CreateCommand();
                    insertReportCmd.CommandText = @"
                    INSERT INTO ListingReport (RoomId, ReporterUserId, Reason, Note, Status, CreatedAt) VALUES
                    (6, 1, 'Phòng đã cho thuê', 'Mình gọi điện cho chủ trọ thì báo phòng này đã có bạn đặt cọc rồi.', 'Pending', '2026-11-09 15:00');";
                    await insertReportCmd.ExecuteNonQueryAsync();
                }
            }
        }

        private static List<RoomListing> GetDefaultIctuRooms() => new()
        {
            // Room 1: Available & Verified
            new RoomListing
            {
                Title = "Phòng khép kín ban công thoáng sát cổng chính ICTU",
                Description = "Phòng xây mới sạch đẹp, có điều hòa, nóng lạnh, giường tủ bàn học đầy đủ. Cách cổng trường ĐH CNTT & TT đúng 300m đi bộ.",
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
                BusMinutes = 5,
                WalkMinutes = 4,
                AmenitiesCsv = "Điều hòa,Nóng lạnh,Wifi cáp quang,Khóa vân tay,Camera 24/7",
                ThumbnailUrl = "dotnet_bot.png",
                PhotosJson = "[\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\"]",
                Panorama360Url = "https://pannellum.org/images/alma.jpg",
                LandlordId = 2,
                LandlordName = "Chú Hòa Chủ Trọ",
                LandlordPhone = "0912333444",
                Status = Constants.ListingStatuses.Available,
                IsVerified = true,
                SecurityScore = 4.8,
                RulesVersion = 2,
                UpdatedAt = "2026-11-09 08:30"
            },
            // Room 2: Available & Verified
            new RoomListing
            {
                Title = "Chung cư mini 28m2 full nội thất hồ điều hòa Tân Thịnh",
                Description = "Phòng rộng rãi cho 2-3 bạn ở ghép, có gác xép cao, kệ bếp nấu ăn riêng, sân phơi có mái che và nhà để xe miễn phí.",
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
                BusMinutes = 7,
                WalkMinutes = 10,
                AmenitiesCsv = "Điều hòa,Nóng lạnh,Tủ lạnh,Máy giặt chung,Gác xép,Bếp riêng",
                ThumbnailUrl = "dotnet_bot.png",
                PhotosJson = "[\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\"]",
                Panorama360Url = "https://pannellum.org/images/cerro-toco-0.jpg",
                LandlordId = 2,
                LandlordName = "Cô Lan Tân Thịnh",
                LandlordPhone = "0977888999",
                Status = Constants.ListingStatuses.Available,
                IsVerified = true,
                SecurityScore = 4.7,
                UpdatedAt = "2026-11-09 09:15"
            },
            // Room 3: Available & Verified
            new RoomListing
            {
                Title = "Phòng trọ sinh viên giá rẻ khu Quyết Thắng an ninh tốt",
                Description = "Phù hợp sinh viên năm nhất tiết kiệm chi phí. Khu trọ yên tĩnh, chủ nhà thân thiện, điện nước giá dân.",
                MonthlyRent = 950000m,
                Deposit = 500000m,
                Area = 16,
                RoomType = "Phòng khép kín",
                Address = "Xóm Gò Móc, Xã Quyết Thắng, TP. Thái Nguyên",
                Ward = "Quyết Thắng",
                Latitude = 21.5880,
                Longitude = 105.8020,
                DistanceToCampusKm = 0.6,
                MotorbikeMinutes = 2,
                BusMinutes = 6,
                WalkMinutes = 8,
                AmenitiesCsv = "Nóng lạnh,Wifi,Sân để xe,Camera an ninh",
                ThumbnailUrl = "dotnet_bot.png",
                PhotosJson = "[\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\"]",
                Panorama360Url = "https://pannellum.org/images/alma.jpg",
                LandlordId = 2,
                LandlordName = "Bác Minh Quyết Thắng",
                LandlordPhone = "0944555666",
                Status = Constants.ListingStatuses.Available,
                IsVerified = true,
                SecurityScore = 4.5,
                UpdatedAt = "2026-11-09 10:00"
            },
            // Room 4: Available & Verified
            new RoomListing
            {
                Title = "Phòng Studio cửa sổ lớn đường Z115 gần chợ sinh viên",
                Description = "Ngay sát chợ sinh viên và trạm xe buýt, thuận tiện đi học và sinh hoạt. Giờ giấc tự do có khóa cổng vân tay.",
                MonthlyRent = 1800000m,
                Deposit = 1800000m,
                Area = 24,
                RoomType = "Chung cư mini",
                Address = "Số 145 Đường Z115, Phường Tân Thịnh, TP. Thái Nguyên",
                Ward = "Tân Thịnh",
                Latitude = 21.5840,
                Longitude = 105.8150,
                DistanceToCampusKm = 1.2,
                MotorbikeMinutes = 4,
                BusMinutes = 8,
                WalkMinutes = 15,
                AmenitiesCsv = "Điều hòa,Nóng lạnh,Giường tủ,Bàn học,Khóa vân tay",
                ThumbnailUrl = "dotnet_bot.png",
                PhotosJson = "[\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\"]",
                Panorama360Url = "https://pannellum.org/images/cerro-toco-0.jpg",
                LandlordId = 2,
                LandlordName = "Anh Tuấn BĐS Sinh Viên",
                LandlordPhone = "0933222111",
                Status = Constants.ListingStatuses.Available,
                IsVerified = true,
                SecurityScore = 4.6,
                UpdatedAt = "2026-11-09 11:00"
            },
            // Room 5: Available & Verified
            new RoomListing
            {
                Title = "Phòng ở ghép 2 người khu vực Thịnh Đán gần vòng xuyến",
                Description = "Tìm bạn sinh viên ICTU hiền lành, sạch sẽ ở ghép. Phòng đã có sẵn toàn bộ đồ dùng nấu ăn và tủ lạnh.",
                MonthlyRent = 800000m,
                Deposit = 800000m,
                Area = 25,
                RoomType = "Ở ghép",
                Address = "Tổ 4 Phường Thịnh Đán, TP. Thái Nguyên",
                Ward = "Thịnh Đán",
                Latitude = 21.5765,
                Longitude = 105.8190,
                DistanceToCampusKm = 1.9,
                MotorbikeMinutes = 6,
                BusMinutes = 12,
                WalkMinutes = 24,
                AmenitiesCsv = "Điều hòa,Tủ lạnh,Máy giặt,Bếp từ,Wifi tốc độ cao",
                ThumbnailUrl = "dotnet_bot.png",
                PhotosJson = "[\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\"]",
                Panorama360Url = "https://pannellum.org/images/alma.jpg",
                LandlordId = 2,
                LandlordName = "Cô Hạnh Thịnh Đán",
                LandlordPhone = "0966777888",
                Status = Constants.ListingStatuses.Available,
                IsVerified = true,
                SecurityScore = 4.4,
                UpdatedAt = "2026-11-09 14:20"
            },
            // Room 6: UNAVAILABLE (For US03 AC3 testing: unavailable room handling & similar rooms suggestion)
            new RoomListing
            {
                Title = "[ĐÃ CHO THUÊ] Phòng khép kín tầng 2 sát giảng đường Z115",
                Description = "Phòng này hiện đã có sinh viên đặt cọc thuê. Hệ thống dùng để kiểm thử tính năng thông báo hết phòng và gợi ý các phòng tương tự cùng tầm giá (US03 - AC3).",
                MonthlyRent = 1400000m,
                Deposit = 1400000m,
                Area = 20,
                RoomType = "Phòng khép kín",
                Address = "Ngõ 18 Đường Z115, Xã Quyết Thắng, TP. Thái Nguyên",
                Ward = "Quyết Thắng",
                Latitude = 21.5858,
                Longitude = 105.8080,
                DistanceToCampusKm = 0.4,
                MotorbikeMinutes = 2,
                BusMinutes = 5,
                WalkMinutes = 5,
                AmenitiesCsv = "Điều hòa,Nóng lạnh,Wifi,Bàn học",
                ThumbnailUrl = "dotnet_bot.png",
                PhotosJson = "[\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\"]",
                Panorama360Url = "https://pannellum.org/images/alma.jpg",
                LandlordId = 2,
                LandlordName = "Chú Hòa Chủ Trọ",
                LandlordPhone = "0912333444",
                Status = Constants.ListingStatuses.Unavailable,
                IsVerified = true,
                SecurityScore = 4.6,
                UpdatedAt = "2026-11-09 15:00"
            },
            // Room 7: PENDING REVIEW (For US09 AC2 testing: newly published landlord room waiting for verification)
            new RoomListing
            {
                Title = "[CHỜ DUYỆT] Phòng mới xây 25m2 có ban công hướng hồ Quyết Thắng",
                Description = "Tin đăng mới của chủ trọ đang ở trạng thái PendingReview chờ Admin ICTU xác thực trước khi hiển thị công khai (US09 - AC2).",
                MonthlyRent = 1600000m,
                Deposit = 1600000m,
                Area = 25,
                RoomType = "Phòng khép kín",
                Address = "Xóm Tân Tiến, Xã Quyết Thắng, TP. Thái Nguyên",
                Ward = "Quyết Thắng",
                Latitude = 21.5870,
                Longitude = 105.8050,
                DistanceToCampusKm = 0.5,
                MotorbikeMinutes = 2,
                BusMinutes = 6,
                WalkMinutes = 6,
                AmenitiesCsv = "Điều hòa,Nóng lạnh,Ban công,Khóa vân tay",
                ThumbnailUrl = "dotnet_bot.png",
                PhotosJson = "[\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\"]",
                Panorama360Url = "https://pannellum.org/images/cerro-toco-0.jpg",
                LandlordId = 2,
                LandlordName = "Chú Hòa Chủ Trọ",
                LandlordPhone = "0912333444",
                Status = Constants.ListingStatuses.PendingReview,
                IsVerified = false,
                SecurityScore = 4.5,
                UpdatedAt = "2026-11-09 16:00"
            }
        };
    }
}
