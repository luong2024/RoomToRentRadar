using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Infrastructure.Adapters.Mocks;

/// <summary>
/// Dữ liệu mẫu 8 phòng quanh ICTU (đồng bộ 100% với
/// <c>RoomToRentRadar.Datastore.Sqlite/Context/RoomSeedData.cs</c>).
/// </summary>
internal static class RoomSeedData
{
    public const string LandlordId = "seed-landlord";

    private static readonly DateTime BaseCreatedAtUtc = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    public static IReadOnlyList<Room> CreateRooms() => new List<Room>(8)
    {
        Create(0, "seed-z115", "Phòng trọ Sinh viên Z115", "Phòng sạch sẽ, gần trường ICTU",
            1_500_000m, 18d, "Z115, phường Tân Thịnh, Thái Nguyên", 21.5875, 105.8080),
        Create(1, "seed-tan-thinh", "Chung cư mini Tân Thịnh", "Chung cư mini tiện nghi",
            2_800_000m, 28d, "Tân Thịnh, Thái Nguyên", 21.5790, 105.8120),
        Create(2, "seed-thao-nguyen", "Nhà trọ Thảo Nguyên - Quyết Thắng", "Nhà trọ yên tĩnh",
            1_200_000m, 15d, "Quyết Thắng, Thái Nguyên", 21.5900, 105.8020),
        Create(3, "seed-thinh-dan", "Phòng cao cấp Thịnh Đán", "Phòng cao cấp đầy đủ tiện nghi",
            3_500_000m, 35d, "Thịnh Đán, Thái Nguyên", 21.5710, 105.8190),
        Create(4, "seed-ktx-32", "Ký túc xá tư nhân Đường 3/2", "Ký túc xá tư nhân",
            2_000_000m, 22d, "Đường 3/2, Thái Nguyên", 21.5600, 105.8250),
        Create(5, "seed-dong-hy", "Nhà trọ sinh viên giá rẻ Đồng Hỷ", "Giá rẻ cho sinh viên",
            900_000m, 12d, "Chợ Điện Đồng Hỷ, Thái Nguyên", 21.6200, 105.8500),
        Create(6, "seed-chua-xac-minh", "Nhà trọ chưa xác minh", "Đang chờ Admin duyệt",
            1_300_000m, 16d, "Tân Thịnh, Thái Nguyên", 21.5860, 105.8070, isVerified: false),
        Create(7, "seed-da-thue", "Nhà trọ đã thuê", "Đã có người thuê",
            1_400_000m, 17d, "Tân Thịnh, Thái Nguyên", 21.5862, 105.8072, isAvailable: false)
    };

    private static Room Create(
        int minuteOffset,
        string id,
        string title,
        string description,
        decimal price,
        double area,
        string address,
        double latitude,
        double longitude,
        bool isVerified = true,
        bool isAvailable = true) => new()
    {
        Id = id,
        Title = title,
        Description = description,
        Price = price,
        Area = area,
        Address = address,
        Latitude = latitude,
        Longitude = longitude,
        LandlordId = LandlordId,
        IsAvailable = isAvailable,
        IsVerified = isVerified,
        CreatedAtUtc = BaseCreatedAtUtc.AddMinutes(minuteOffset)
    };
}
