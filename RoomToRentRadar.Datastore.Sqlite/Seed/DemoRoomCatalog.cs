using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Datastore.Sqlite.Seed;

/// <summary>
/// T00.10 extended demo catalog: enriches the 8 core rooms from <see cref="RoomSeedData"/> with
/// amenities, safety features, and house rules, and adds 22 extra rooms (<c>demo-01</c>..<c>demo-22</c>)
/// around ICTU, TNUT, TNUE, and TUMP.
/// </summary>
public static class DemoRoomCatalog
{
    private enum ChildProfile
    {
        Basic,
        Standard,
        Premium
    }

    public sealed record SeededRoomEntry(Room Room, IReadOnlyList<HouseRuleVersion> HouseRuleVersions);

    private static readonly DateTime BaseCreatedAtUtc = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly BaseAvailableFrom = new(2026, 11, 1);

    private static readonly Amenity[] BasicAmenities =
    {
        Amenity.Window,
        Amenity.Bed,
        Amenity.Wifi
    };

    private static readonly Amenity[] StandardAmenities =
    {
        Amenity.PrivateBathroom,
        Amenity.WaterHeater,
        Amenity.Wifi,
        Amenity.Bed,
        Amenity.Wardrobe,
        Amenity.Window,
        Amenity.MotorbikeParking
    };

    private static readonly Amenity[] PremiumAmenities =
    {
        Amenity.AirConditioner,
        Amenity.WaterHeater,
        Amenity.PrivateBathroom,
        Amenity.Wifi,
        Amenity.Kitchen,
        Amenity.Fridge,
        Amenity.WashingMachine,
        Amenity.Bed,
        Amenity.Wardrobe,
        Amenity.Desk,
        Amenity.Balcony,
        Amenity.Elevator,
        Amenity.MotorbikeParking
    };

    private static readonly SafetyFeature[] BasicSafetyFeatures =
    {
        SafetyFeature.SecureEntrance
    };

    private static readonly SafetyFeature[] StandardSafetyFeatures =
    {
        SafetyFeature.Cctv,
        SafetyFeature.SecureEntrance,
        SafetyFeature.WellLitAccess
    };

    private static readonly SafetyFeature[] PremiumSafetyFeatures =
    {
        SafetyFeature.Cctv,
        SafetyFeature.SecureEntrance,
        SafetyFeature.FireSafety,
        SafetyFeature.OwnerOrGuardOnSite,
        SafetyFeature.WellLitAccess
    };

    private static readonly IReadOnlyDictionary<string, ChildProfile> CoreRoomProfiles =
        new Dictionary<string, ChildProfile>(StringComparer.Ordinal)
        {
            ["seed-z115"] = ChildProfile.Standard,
            ["seed-tan-thinh"] = ChildProfile.Premium,
            ["seed-thao-nguyen"] = ChildProfile.Basic,
            ["seed-thinh-dan"] = ChildProfile.Premium,
            ["seed-ktx-32"] = ChildProfile.Basic,
            ["seed-chua-xac-minh"] = ChildProfile.Basic,
            ["seed-da-thue"] = ChildProfile.Standard,
            ["seed-dong-hy"] = ChildProfile.Basic
        };

    public static IReadOnlyList<Room> CreateCoreRoomsWithChildren()
    {
        var rooms = RoomSeedData.CreateRooms();
        foreach (var room in rooms)
        {
            var profile = CoreRoomProfiles[room.Id];
            var versions = BuildHouseRuleVersions(room, profile);
            ApplyChildren(room, profile, versions[^1]);
        }

        return rooms;
    }

    public static IReadOnlyList<Room> CreateExtraRooms() =>
        CreateExtraEntries().Select(e => e.Room).ToList();

    public static IReadOnlyList<SeededRoomEntry> CreateAllEntries()
    {
        var entries = new List<SeededRoomEntry>(30);
        foreach (var room in CreateCoreRoomsWithChildren())
        {
            var profile = CoreRoomProfiles[room.Id];
            entries.Add(new SeededRoomEntry(room, BuildHouseRuleVersions(room, profile)));
        }

        entries.AddRange(CreateExtraEntries());
        return entries;
    }

    private static IReadOnlyList<SeededRoomEntry> CreateExtraEntries()
    {
        var campuses = CampusSeedData.CreateCampuses().ToDictionary(c => c.Id, StringComparer.Ordinal);

        return new List<SeededRoomEntry>(22)
        {
            // ICTU cluster (demo-01..05)
            BuildExtraEntry(1, "demo-01", "Phòng trọ tiết kiệm ngõ Quyết Thắng ICTU", campuses["ICTU"], +0.0020, -0.0040,
                RoomType.BoardingRoom, 1_100_000L, 14.0, ListingStatus.Available, ChildProfile.Basic,
                "Ngõ 45 Đường Z115", "Quyết Thắng", 2, "Basic", "Phòng gọn gàng, đi bộ ra cổng ICTU chỉ 5 phút."),
            BuildExtraEntry(2, "demo-02", "Phòng trọ khép kín gần ĐH CNTT&TT", campuses["ICTU"], -0.0030, +0.0050,
                RoomType.BoardingRoom, 1_600_000L, 20.0, ListingStatus.Available, ChildProfile.Standard,
                "Tổ 8 Đường Tân Thịnh", "Tân Thịnh", 2, "Basic", "Phòng sáng thoáng, để xe tầng 1 có camera giám sát."),
            BuildExtraEntry(3, "demo-03", "Chung cư mini ban công thoáng Tân Thịnh", campuses["ICTU"], +0.0055, +0.0030,
                RoomType.MiniApartment, 2_400_000L, 25.0, ListingStatus.Available, ChildProfile.Standard,
                "Ngõ 18 Đường Quang Trung", "Tân Thịnh", 3, "Full", "Căn hộ mini mới sơn sửa, gần siêu thị và bến buýt."),
            BuildExtraEntry(4, "demo-04", "Ký túc xá sinh viên Quyết Thắng giá tốt", campuses["ICTU"], -0.0060, -0.0035,
                RoomType.Dormitory, 950_000L, 12.0, ListingStatus.Available, ChildProfile.Basic,
                "Tổ 4 Đường Z115", "Quyết Thắng", 4, "Basic", "Giường tầng sạch sẽ, có khu sinh hoạt chung cho sinh viên."),
            BuildExtraEntry(5, "demo-05", "Nhà nguyên căn 2 tầng gần ICTU Tân Thịnh", campuses["ICTU"], +0.0075, -0.0060,
                RoomType.WholeHouse, 4_000_000L, 60.0, ListingStatus.Rented, ChildProfile.Premium,
                "Ngõ 32 Đường Tân Thịnh", "Tân Thịnh", 5, "Full", "Nhà nguyên căn phù hợp nhóm 4-5 bạn ở chung."),

            // TNUT cluster (demo-06..11)
            BuildExtraEntry(6, "demo-06", "Phòng trọ sinh viên ngõ Tích Lương TNUT", campuses["TNUT"], +0.0010, +0.0015,
                RoomType.BoardingRoom, 1_000_000L, 15.0, ListingStatus.Available, ChildProfile.Basic,
                "Ngõ 102 Đường 3/2", "Tích Lương", 2, "Unfurnished", "Phòng giá bình dân ngay sát cổng ĐH Kỹ thuật Công nghiệp."),
            BuildExtraEntry(7, "demo-07", "Phòng trọ mới xây có gác xép Tích Lương", campuses["TNUT"], -0.0035, +0.0040,
                RoomType.BoardingRoom, 1_350_000L, 18.0, ListingStatus.Available, ChildProfile.Standard,
                "Tổ 6 Đường 3/2", "Tích Lương", 2, "Basic", "Có gác xép rộng, WC riêng, khu vực an ninh tốt."),
            BuildExtraEntry(8, "demo-08", "Chung cư mini đầy đủ tiện nghi gần TNUT", campuses["TNUT"], +0.0050, -0.0045,
                RoomType.MiniApartment, 2_200_000L, 24.0, ListingStatus.Available, ChildProfile.Standard,
                "Ngõ 55 Đường Cách Mạng Tháng 8", "Trung Thành", 3, "Full", "Phòng rộng rãi có bình nóng lạnh và chỗ để xe miễn phí."),
            BuildExtraEntry(9, "demo-09", "Ký túc xá tư nhân Trung Thành gần ĐH KTCN", campuses["TNUT"], -0.0060, -0.0020,
                RoomType.Dormitory, 900_000L, 10.0, ListingStatus.Available, ChildProfile.Basic,
                "Tổ 11 Phường Trung Thành", "Trung Thành", 4, "Basic", "Phòng ghép tiết kiệm chi phí, giờ giấc ổn định."),
            BuildExtraEntry(10, "demo-10", "Nhà nguyên căn sân rộng gần ĐH KTCN", campuses["TNUT"], +0.0070, +0.0060,
                RoomType.WholeHouse, 3_800_000L, 55.0, ListingStatus.Available, ChildProfile.Premium,
                "Tổ 2 Phường Tích Lương", "Tích Lương", 5, "Full", "Nhà 2 phòng ngủ đầy đủ nội thất, sân để xe rộng."),
            BuildExtraEntry(11, "demo-11", "Căn hộ mini mới đăng chờ duyệt gần TNUT", campuses["TNUT"], -0.0020, -0.0070,
                RoomType.MiniApartment, 2_900_000L, 30.0, ListingStatus.PendingReview, ChildProfile.Standard,
                "Ngõ 88 Đường 3/2", "Tích Lương", 3, "Full", "Tin đăng mới của chủ trọ đang chờ Admin duyệt."),

            // TNUE cluster (demo-12..17)
            BuildExtraEntry(12, "demo-12", "Phòng trọ yên tĩnh gần ĐH Sư phạm TNUE", campuses["TNUE"], +0.0015, -0.0025,
                RoomType.BoardingRoom, 1_250_000L, 16.0, ListingStatus.Available, ChildProfile.Standard,
                "Ngõ 14 Đường Lương Ngọc Quyến", "Quang Trung", 2, "Basic", "Khu trọ yên tĩnh, nhiều sinh viên Sư phạm sinh sống."),
            BuildExtraEntry(13, "demo-13", "Phòng trọ cửa sổ lớn phường Quang Trung", campuses["TNUE"], -0.0040, +0.0030,
                RoomType.BoardingRoom, 1_800_000L, 22.0, ListingStatus.Available, ChildProfile.Standard,
                "Tổ 15 Phường Quang Trung", "Quang Trung", 2, "Basic", "Phòng thoáng mát, gần chợ Đồng Quang và bến xe."),
            BuildExtraEntry(14, "demo-14", "Chung cư mini cao cấp trung tâm gần TNUE", campuses["TNUE"], +0.0055, +0.0045,
                RoomType.MiniApartment, 3_200_000L, 32.0, ListingStatus.Available, ChildProfile.Premium,
                "Số 22 Ngõ 5 Đường Bắc Kạn", "Hoàng Văn Thụ", 3, "Full", "Thang máy, khóa vân tay, điều hòa và máy giặt đầy đủ."),
            BuildExtraEntry(15, "demo-15", "Ký túc xá nữ gần ĐH Sư phạm Thái Nguyên", campuses["TNUE"], -0.0065, -0.0050,
                RoomType.Dormitory, 1_000_000L, 11.0, ListingStatus.Available, ChildProfile.Basic,
                "Tổ 7 Phường Đồng Quang", "Đồng Quang", 4, "Basic", "Ký túc xá sạch đẹp, đi bộ 7 phút tới cổng TNUE."),
            BuildExtraEntry(16, "demo-16", "Nhà nguyên căn trung tâm Hoàng Văn Thụ", campuses["TNUE"], +0.0030, -0.0070,
                RoomType.WholeHouse, 3_500_000L, 48.0, ListingStatus.Rented, ChildProfile.Premium,
                "Ngõ 19 Đường Minh Cầu", "Hoàng Văn Thụ", 4, "Full", "Nhà sạch đẹp ngay trung tâm thành phố, đã có người thuê."),
            BuildExtraEntry(17, "demo-17", "Phòng trọ Đồng Quang cần bổ sung thông tin", campuses["TNUE"], -0.0010, +0.0075,
                RoomType.BoardingRoom, 1_450_000L, 17.0, ListingStatus.Rejected, ChildProfile.Basic,
                "Ngõ 63 Đường Phan Đình Phùng", "Đồng Quang", 2, "Basic", "Phòng gần ngã tư trung tâm, cần cập nhật lại mô tả.",
                statusReason: "Địa chỉ ghim bản đồ chưa khớp mô tả chi tiết (BR-LST-04)."),

            // TUMP cluster (demo-18..22)
            BuildExtraEntry(18, "demo-18", "Phòng trọ sinh viên Y Dược ngõ Lương Ngọc Quyến", campuses["TUMP"], +0.0020, +0.0020,
                RoomType.BoardingRoom, 1_150_000L, 15.0, ListingStatus.Available, ChildProfile.Basic,
                "Ngõ 110 Lương Ngọc Quyến", "Phan Đình Phùng", 2, "Basic", "Gần bệnh viện Đa khoa và ĐH Y Dược Thái Nguyên."),
            BuildExtraEntry(19, "demo-19", "Chung cư mini gần ĐH Y Dược Phan Đình Phùng", campuses["TUMP"], -0.0045, +0.0035,
                RoomType.MiniApartment, 2_600_000L, 27.0, ListingStatus.Available, ChildProfile.Standard,
                "Tổ 14 Phường Phan Đình Phùng", "Phan Đình Phùng", 3, "Full", "Phòng khép kín tiện nghi, phù hợp sinh viên Y thực tập."),
            BuildExtraEntry(20, "demo-20", "Phòng trọ an ninh tốt gần cổng ĐH Y Dược", campuses["TUMP"], +0.0050, -0.0030,
                RoomType.BoardingRoom, 1_700_000L, 21.0, ListingStatus.Available, ChildProfile.Standard,
                "Ngõ 78 Đường Đội Cấn", "Hoàng Văn Thụ", 2, "Basic", "Có camera hành lang, giờ giấc tự do, khu dân trí cao."),
            BuildExtraEntry(21, "demo-21", "Nhà nguyên căn rộng rãi gần ĐH Y Dược TUMP", campuses["TUMP"], -0.0020, -0.0060,
                RoomType.WholeHouse, 4_000_000L, 65.0, ListingStatus.Available, ChildProfile.Premium,
                "Ngõ 25 Đường Hoàng Văn Thụ", "Phan Đình Phùng", 6, "Full", "Nhà 3 phòng ngủ thoáng rộng, đầy đủ thiết bị sinh hoạt."),
            BuildExtraEntry(22, "demo-22", "Căn hộ mini Trưng Vương chờ kiểm duyệt", campuses["TUMP"], +0.0065, +0.0065,
                RoomType.MiniApartment, 2_000_000L, 23.0, ListingStatus.PendingReview, ChildProfile.Basic,
                "Tổ 3 Phường Trưng Vương", "Trưng Vương", 2, "Basic", "Tin đăng mới khu Trưng Vương đang chờ xác minh.")
        };
    }

    private static SeededRoomEntry BuildExtraEntry(
        int demoNumber,
        string id,
        string title,
        Campus campus,
        double dLat,
        double dLng,
        RoomType roomType,
        long monthlyRentVnd,
        double areaM2,
        ListingStatus status,
        ChildProfile profile,
        string addressLine,
        string ward,
        int maxOccupants,
        string furnishing,
        string description,
        string? statusReason = null)
    {
        var createdAtUtc = BaseCreatedAtUtc.AddMinutes(9 + demoNumber);
        var verifiedAtUtc = status is ListingStatus.Available or ListingStatus.Rented
            ? createdAtUtc.AddMinutes(30)
            : (DateTime?)null;
        var publishedAtUtc = status == ListingStatus.Available
            ? verifiedAtUtc
            : null;
        var isSafetyVerified = status is ListingStatus.Available or ListingStatus.Rented &&
                               profile is ChildProfile.Standard or ChildProfile.Premium;

        var room = Room.Reconstitute(
            id: id,
            landlordId: RoomSeedData.LandlordId,
            title: title,
            roomType: roomType,
            monthlyRentVnd: monthlyRentVnd,
            depositVnd: monthlyRentVnd,
            areaM2: areaM2,
            addressLine: addressLine,
            ward: ward,
            district: "TP. Thái Nguyên",
            latitude: Math.Round(campus.Latitude + dLat, 6),
            longitude: Math.Round(campus.Longitude + dLng, 6),
            status: status,
            createdAtUtc: createdAtUtc,
            lastUpdatedAtUtc: createdAtUtc,
            version: 1,
            description: description,
            maxOccupants: maxOccupants,
            furnishing: furnishing,
            availableFrom: BaseAvailableFrom,
            electricityPriceVndPerKwh: profile == ChildProfile.Premium ? 4_000L : 3_500L,
            waterPriceVnd: profile == ChildProfile.Premium ? 25_000L : 50_000L,
            waterPriceUnit: profile == ChildProfile.Premium ? "PerCubicMeter" : "PerPerson",
            internetFeeVnd: profile == ChildProfile.Premium ? 0L : 80_000L,
            parkingFeeVnd: profile == ChildProfile.Basic ? 0L : 50_000L,
            otherFeesNote: profile == ChildProfile.Basic ? null : "Vệ sinh chung 20.000 đ/tháng",
            statusReason: statusReason,
            needsReReview: false,
            isSafetyVerified: isSafetyVerified,
            verifiedAtUtc: verifiedAtUtc,
            verifiedByUserId: null,
            publishedAtUtc: publishedAtUtc);

        var versions = BuildHouseRuleVersions(room, profile);
        ApplyChildren(room, profile, versions[^1]);

        return new SeededRoomEntry(room, versions);
    }

    private static void ApplyChildren(Room room, ChildProfile profile, HouseRuleVersion currentRules)
    {
        var amenities = profile switch
        {
            ChildProfile.Basic => BasicAmenities,
            ChildProfile.Standard => StandardAmenities,
            ChildProfile.Premium => PremiumAmenities,
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };

        var features = profile switch
        {
            ChildProfile.Basic => BasicSafetyFeatures,
            ChildProfile.Standard => StandardSafetyFeatures,
            ChildProfile.Premium => PremiumSafetyFeatures,
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };

        var safetyEntries = features
            .Select(f => new RoomSafetyFeatureEntry(f, room.IsSafetyVerified))
            .ToArray();

        room.SetChildren(amenities, safetyEntries, currentRules);
    }

    private static IReadOnlyList<HouseRuleVersion> BuildHouseRuleVersions(Room room, ChildProfile profile)
    {
        if (string.Equals(room.Id, "demo-02", StringComparison.Ordinal))
        {
            var v1 = HouseRuleVersion.Reconstitute(
                id: "hr-demo-02-v1",
                roomId: room.Id,
                versionNumber: 1,
                guestPolicy: GuestPolicy.DaytimeOnly,
                cookingPolicy: CookingPolicy.Allowed,
                parkingAvailable: true,
                petsAllowed: false,
                smokingAllowed: false,
                createdByUserId: RoomSeedData.LandlordId,
                createdAtUtc: room.CreatedAtUtc,
                quietHoursStart: "23:00",
                quietHoursEnd: "06:00",
                curfewTime: "23:00",
                parkingRules: "Xếp xe máy gọn gàng trong sân tầng 1.");

            var v2 = HouseRuleVersion.Reconstitute(
                id: "hr-demo-02-v2",
                roomId: room.Id,
                versionNumber: 2,
                guestPolicy: GuestPolicy.DaytimeOnly,
                cookingPolicy: CookingPolicy.Allowed,
                parkingAvailable: true,
                petsAllowed: false,
                smokingAllowed: false,
                createdByUserId: RoomSeedData.LandlordId,
                createdAtUtc: room.CreatedAtUtc.AddMinutes(5),
                quietHoursStart: "23:00",
                quietHoursEnd: "06:00",
                curfewTime: "23:30",
                parkingRules: "Xếp xe máy gọn gàng trong sân tầng 1.");

            return new[] { v1, v2 };
        }

        var rule = profile switch
        {
            ChildProfile.Basic => HouseRuleVersion.Reconstitute(
                id: $"hr-{room.Id}-v1",
                roomId: room.Id,
                versionNumber: 1,
                guestPolicy: GuestPolicy.DaytimeOnly,
                cookingPolicy: CookingPolicy.SharedKitchenOnly,
                parkingAvailable: true,
                petsAllowed: false,
                smokingAllowed: false,
                createdByUserId: RoomSeedData.LandlordId,
                createdAtUtc: room.CreatedAtUtc,
                quietHoursStart: "22:30",
                quietHoursEnd: "06:00",
                curfewTime: "23:30"),
            ChildProfile.Standard => HouseRuleVersion.Reconstitute(
                id: $"hr-{room.Id}-v1",
                roomId: room.Id,
                versionNumber: 1,
                guestPolicy: GuestPolicy.DaytimeOnly,
                cookingPolicy: CookingPolicy.Allowed,
                parkingAvailable: true,
                petsAllowed: false,
                smokingAllowed: false,
                createdByUserId: RoomSeedData.LandlordId,
                createdAtUtc: room.CreatedAtUtc,
                quietHoursStart: "23:00",
                quietHoursEnd: "06:00",
                curfewTime: null,
                parkingRules: "Xếp xe máy đúng vạch tại tầng 1."),
            ChildProfile.Premium => HouseRuleVersion.Reconstitute(
                id: $"hr-{room.Id}-v1",
                roomId: room.Id,
                versionNumber: 1,
                guestPolicy: GuestPolicy.Allowed,
                cookingPolicy: CookingPolicy.Allowed,
                parkingAvailable: true,
                petsAllowed: true,
                smokingAllowed: false,
                createdByUserId: RoomSeedData.LandlordId,
                createdAtUtc: room.CreatedAtUtc,
                quietHoursStart: null,
                quietHoursEnd: null,
                curfewTime: null,
                otherRules: "Không hút thuốc trong phòng"),
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };

        return new[] { rule };
    }
}
