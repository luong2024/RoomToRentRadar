using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Client.Adapters.Mocks.SampleData;

/// <summary>
/// Fixed copy of the five Available rooms in <c>RoomSeedData</c> (PLAN Q6/Q11): ids, titles, rents,
/// areas, types, coordinates, PublishedAtUtc, descriptions, wards and districts mirror the seed exactly
/// so MockSeedDriftTests and MockVsRealApiParityTests stay green; they break on purpose when the seed changes.
/// No DateTime.UtcNow/Random/Guid.NewGuid — the data is fully deterministic.
/// </summary>
public static class SampleRooms
{
    public static IReadOnlyList<SampleRoom> All { get; } = new List<SampleRoom>
    {
        new("seed-z115", "Phòng trọ Sinh viên Z115",
            "Phòng trọ sạch sẽ ngay sau ĐH CNTT&TT, gần chợ và bến xe buýt.",
            RoomType.BoardingRoom, 1_500_000L, 1_500_000L, 18d, 2, Furnishing.Basic, new DateOnly(2026, 11, 1),
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
            3_500L, 60_000L, WaterPriceUnit.PerPerson, 100_000L, 50_000L, "Rác 20.000 đ/tháng",
            true, new DateTime(2026, 11, 1, 8, 30, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc)),
        new("seed-tan-thinh", "Chung cư mini Tân Thịnh",
            "Căn hộ mini có ban công, thang máy, an ninh 24/7.",
            RoomType.MiniApartment, 2_800_000L, 2_800_000L, 28d, 3, Furnishing.Full, new DateOnly(2026, 11, 1),
            "Ngõ 12", "Tân Thịnh", "TP. Thái Nguyên", 21.5790, 105.8120,
            4_000L, 25_000L, WaterPriceUnit.PerCubicMeter, 0L, 150_000L, null,
            true, new DateTime(2026, 11, 1, 8, 31, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 8, 1, 0, DateTimeKind.Utc)),
        new("seed-thao-nguyen", "Nhà trọ Thảo Nguyên - Quyết Thắng",
            "Phòng giá rẻ phù hợp sinh viên, có gác xép, gần ĐH Nông Lâm.",
            RoomType.BoardingRoom, 1_200_000L, 1_200_000L, 15d, 2, Furnishing.Unfurnished, new DateOnly(2026, 11, 1),
            "Tổ 3", "Quyết Thắng", "TP. Thái Nguyên", 21.5900, 105.8020,
            3_500L, 50_000L, WaterPriceUnit.PerPerson, 80_000L, 0L, "Điện nước tính theo đồng hồ riêng",
            false, new DateTime(2026, 11, 1, 8, 32, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 8, 2, 0, DateTimeKind.Utc)),
        new("seed-thinh-dan", "Phòng cao cấp Thịnh Đán",
            "Phòng mới 100%, máy lạnh, nóng lạnh, đầy đủ nội thất.",
            RoomType.MiniApartment, 3_500_000L, 3_500_000L, 35d, 4, Furnishing.Full, new DateOnly(2026, 11, 1),
            "Tổ 5", "Thịnh Đán", "TP. Thái Nguyên", 21.5710, 105.8190,
            4_000L, 100_000L, WaterPriceUnit.Included, 0L, 100_000L, null,
            true, new DateTime(2026, 11, 1, 8, 33, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 8, 3, 0, DateTimeKind.Utc)),
        new("seed-ktx-32", "Ký túc xá tư nhân Đường 3/2",
            "Phòng ở ghép 4 người, WC khép kín, có khu tự học và giặt sấy.",
            RoomType.Dormitory, 2_000_000L, 2_000_000L, 22d, 4, Furnishing.Basic, new DateOnly(2026, 11, 1),
            "Đường 3/2", "Phan Đình Phùng", "TP. Thái Nguyên", 21.5600, 105.8250,
            3_500L, 40_000L, WaterPriceUnit.PerPerson, 0L, 0L, "Phí giặt sấy 30.000 đ/tháng",
            true, new DateTime(2026, 11, 1, 8, 34, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 8, 4, 0, DateTimeKind.Utc))
    };

    public static SampleRoom? Find(string? id) => All.FirstOrDefault(room => room.Id == id);
}

/// <summary>One demo room: the seed scalars the mock cards and detail views are built from.</summary>
public sealed record SampleRoom(
    string Id,
    string Title,
    string? Description,
    RoomType RoomType,
    long MonthlyRentVnd,
    long DepositVnd,
    double AreaM2,
    int MaxOccupants,
    Furnishing Furnishing,
    DateOnly AvailableFrom,
    string AddressLine,
    string Ward,
    string District,
    double Latitude,
    double Longitude,
    long ElectricityPriceVndPerKwh,
    long WaterPriceVnd,
    WaterPriceUnit WaterPriceUnit,
    long InternetFeeVnd,
    long ParkingFeeVnd,
    string? OtherFeesNote,
    bool IsSafetyVerified,
    DateTime PublishedAtUtc,
    DateTime LastUpdatedAtUtc);