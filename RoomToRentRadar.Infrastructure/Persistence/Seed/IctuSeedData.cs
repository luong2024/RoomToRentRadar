using RoomToRentRadar.Models;

namespace RoomToRentRadar.Infrastructure.Persistence.Seed
{
    /// <summary>
    /// Canonical sample data around ICTU campus (OWNER: @architect).
    /// Single source of truth used by BOTH the SQLite seeder and the in-memory Fakes,
    /// so UI developers see exactly the same data with or without the real repositories.
    /// Each call returns NEW instances (callers may mutate them safely).
    /// </summary>
    public static class IctuSeedData
    {
        public static List<UserAccount> Users() => new()
        {
            new UserAccount { Id = 1, FullName = "Sinh viên ICTU (Demo)", Email = "student@ictu.edu.vn", PasswordHash = "123456", Phone = "0988111222", Role = Constants.UserRoles.Student, SelectedCampus = "ICTU", CreatedAt = "2026-11-09" },
            new UserAccount { Id = 2, FullName = "Chú Hòa Chủ Trọ Z115", Email = "landlord@ictu.edu.vn", PasswordHash = "123456", Phone = "0912333444", Role = Constants.UserRoles.Landlord, SelectedCampus = "ICTU", CreatedAt = "2026-11-09" },
            new UserAccount { Id = 3, FullName = "Ban Quản Lý Nội Trú ICTU", Email = "admin@ictu.edu.vn", PasswordHash = "123456", Phone = "0909555666", Role = Constants.UserRoles.Admin, SelectedCampus = "ICTU", CreatedAt = "2026-11-09" },
        };

        public static List<HouseRuleVersion> HouseRules() => new()
        {
            new HouseRuleVersion { Id = 1, RoomId = 1, VersionNumber = 1, QuietHours = "22:00 - 05:30", GuestPolicy = "Không tiếp khách sau 22:00", CookingRules = "Được nấu ăn bằng bếp điện", ParkingRules = "Mỗi phòng 1 xe máy", UpdatedAt = "2026-10-01 08:00" },
            new HouseRuleVersion { Id = 2, RoomId = 1, VersionNumber = 2, QuietHours = "22:30 - 05:30", GuestPolicy = "Khách đến chơi báo trước, không ở qua đêm sau 22:30", CookingRules = "Được nấu ăn bằng bếp từ hoặc bếp hồng ngoại", ParkingRules = "Mỗi phòng tối đa 2 xe máy tại nhà xe có camera", UpdatedAt = "2026-11-09 08:30" },
        };

        public static List<ListingReport> Reports() => new()
        {
            new ListingReport { Id = 1, RoomId = 6, ReporterUserId = 1, Reason = "Phòng đã cho thuê", Note = "Mình gọi điện cho chủ trọ thì báo phòng này đã có bạn đặt cọc rồi.", Status = Constants.ReportStatuses.Pending, CreatedAt = "2026-11-09 15:00" },
        };

        /// <summary>
        /// 7 sample rooms across all 3 statuses (Available, Unavailable, PendingReview).
        /// Ids are assigned 1..7 in order (matches SQLite AUTOINCREMENT on a fresh database).
        /// </summary>
        public static List<RoomListing> Rooms()
        {
            var rooms = new List<RoomListing>
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

            for (var i = 0; i < rooms.Count; i++)
                rooms[i].Id = i + 1;

            return rooms;
        }
    }
}
