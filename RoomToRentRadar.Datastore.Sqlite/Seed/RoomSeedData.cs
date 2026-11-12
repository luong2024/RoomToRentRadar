using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Datastore.Sqlite.Seed;

/// <summary>
/// Temporary demo seed (H4/M5). Task 3 (Giai đoạn 3) rewrites the rows for the ERD §4.4 schema
/// (5 Available, 1 PendingReview, 1 Rented, 1 Rejected) and keeps it behind
/// <c>SqliteSettings.SeedDemoRooms</c>; <c>CampusSeedData</c> is added at the same time.
/// </summary>
public static class RoomSeedData
{
    public const string LandlordId = "seed-landlord";

    private static readonly DateTime BaseCreatedAtUtc = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    public static IReadOnlyList<Room> CreateRooms() => new List<Room>(8)
    {
        Create(0, "seed-z115", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 18d, "Z115", 21.5875, 105.8080, ListingStatus.Available, BaseCreatedAtUtc),
        Create(1, "seed-tan-thinh", "Chung cư mini Tân Thịnh", RoomType.MiniApartment, 2_800_000L, 28d, "Ngõ 12", 21.5790, 105.8120, ListingStatus.Available, BaseCreatedAtUtc),
        Create(2, "seed-thao-nguyen", "Nhà trọ Thảo Nguyên - Quyết Thắng", RoomType.BoardingRoom, 1_200_000L, 15d, "Tổ 3", 21.5900, 105.8020, ListingStatus.Available, BaseCreatedAtUtc),
        Create(3, "seed-thinh-dan", "Phòng cao cấp Thịnh Đán", RoomType.MiniApartment, 3_500_000L, 35d, "Tổ 5", 21.5710, 105.8190, ListingStatus.Available, BaseCreatedAtUtc),
        Create(4, "seed-ktx-32", "Ký túc xá tư nhân Đường 3/2", RoomType.Dormitory, 2_000_000L, 22d, "Đường 3/2", 21.5600, 105.8250, ListingStatus.Available, BaseCreatedAtUtc),
        Create(5, "seed-dong-hy", "Nhà trọ sinh viên giá rẻ Đồng Hỷ", RoomType.WholeHouse, 900_000L, 12d, "Thị trấn Chợ Mới", 21.6200, 105.8500, ListingStatus.Available, BaseCreatedAtUtc),
        Create(6, "seed-chua-xac-minh", "Nhà trọ chưa xác minh", RoomType.BoardingRoom, 1_300_000L, 16d, "Tổ 7", 21.5860, 105.8070, ListingStatus.PendingReview, null),
        Create(7, "seed-da-thue", "Nhà trọ đã thuê", RoomType.BoardingRoom, 1_400_000L, 17d, "Tổ 9", 21.5862, 105.8072, ListingStatus.Rented, BaseCreatedAtUtc)
    };

    private static Room Create(
        int minuteOffset,
        string id,
        string title,
        RoomType roomType,
        long monthlyRentVnd,
        double areaM2,
        string addressLine,
        double latitude,
        double longitude,
        ListingStatus status,
        DateTime? verifiedAtUtc)
    {
        var createdAtUtc = BaseCreatedAtUtc.AddMinutes(minuteOffset);

        return Room.Reconstitute(
            id: id,
            landlordId: LandlordId,
            title: title,
            roomType: roomType,
            monthlyRentVnd: monthlyRentVnd,
            depositVnd: monthlyRentVnd,
            areaM2: areaM2,
            addressLine: addressLine,
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: latitude,
            longitude: longitude,
            status: status,
            createdAtUtc: createdAtUtc,
            lastUpdatedAtUtc: createdAtUtc,
            version: 1,
            verifiedAtUtc: verifiedAtUtc,
            publishedAtUtc: verifiedAtUtc,
            statusReason: status == ListingStatus.PendingReview ? null : "Seed demo data");
    }
}
