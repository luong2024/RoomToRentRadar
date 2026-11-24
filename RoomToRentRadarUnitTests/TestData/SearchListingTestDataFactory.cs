using RoomToRentRadar.Api.Models;

namespace RoomToRentRadarUnitTests.TestData;

/// <summary>
/// Tạo dữ liệu chuẩn hóa cho test tổ hợp bộ lọc và hiệu năng tìm kiếm (US04 T04.3).
/// Lưu ý: Mọi ID phải bắt đầu bằng "room-2026-" để khớp bộ lọc _canonical của InMemoryListingRepository.
/// </summary>
public static class SearchListingTestDataFactory
{
    private static readonly string[] RoomTypes = ["Studio", "Khép kín", "Chung cư mini", "Ở ghép"];

    public static IReadOnlyList<ListingDocument> CreateFilterMatrixDataset()
    {
        return
        [
            CreateRoom("room-2026-001", "ictu-main", "Available", true, "Studio", 1_500_000m, 18.0, 500,
                photos:
                [
                    new PhotoDocument { Url = "https://cdn.example/p1_b.jpg", Is360 = false, Order = 2 },
                    new PhotoDocument { Url = "https://cdn.example/p1_a.jpg", Is360 = false, Order = 1 }
                ]),
            CreateRoom("room-2026-002", "ictu-main", "Available", true, "Studio", 2_000_000m, 25.0, 1000, photos: []),
            CreateRoom("room-2026-003", "ictu-main", "Available", true, "Studio", 2_500_000m, 30.0, 2000),
            CreateRoom("room-2026-004", "ictu-main", "Available", true, "Khép kín", 1_500_000m, 22.0, 150),
            CreateRoom("room-2026-005", "ictu-main", "Available", true, "Khép kín", 2_200_000m, 28.0, 800),
            CreateRoom("room-2026-006", "ictu-main", "Available", true, "Chung cư mini", 3_200_000m, 35.0, 1200),
            CreateRoom("room-2026-007", "ictu-main", "Available", true, "Chung cư mini", 4_000_000m, 40.0, 2500),
            CreateRoom("room-2026-008", "ictu-main", "Available", true, "Ở ghép", 1_000_000m, 15.0, 400),
            CreateRoom("room-2026-009", "ictu-main", "Available", true, "Ở ghép", 1_200_000m, 18.0, 900),
            CreateRoom("room-2026-010", "ictu-main", "Available", true, "Studio", 1_800_000m, 20.0, 500),
            CreateRoom("room-2026-011", "ictu-main", "Available", true, "Studio", 2_000_000m, 25.0, 300),
            // Các phòng không hợp lệ cho truy vấn công khai (US01 AC4)
            CreateRoom("room-2026-012", "ictu-main", "Rented", true, "Studio", 1_600_000m, 22.0, 200),
            CreateRoom("room-2026-013", "ictu-main", "PendingReview", true, "Studio", 1_700_000m, 23.0, 250),
            CreateRoom("room-2026-014", "ictu-main", "Available", false, "Studio", 1_400_000m, 21.0, 180),
            // Các phòng thuộc cơ sở khác để kiểm tra cô lập CampusId
            CreateRoom("room-2026-015", "ictu-cs2", "Available", true, "Studio", 1_500_000m, 20.0, 300),
            CreateRoom("room-2026-016", "ictu-cs2", "Available", true, "Khép kín", 2_300_000m, 26.0, 600)
        ];
    }

    public static IReadOnlyList<ListingDocument> CreateLargeDataset(int count)
    {
        var rooms = new List<ListingDocument>(count);
        for (int i = 1; i <= count; i++)
        {
            string campusId = i % 10 == 0 ? "ictu-cs2" : "ictu-main";
            string status = i % 20 == 0 ? "PendingReview" : (i % 10 == 5 ? "Rented" : "Available");
            bool isVerified = i % 10 != 9;
            string roomType = RoomTypes[i % RoomTypes.Length];
            decimal rent = 900_000m + (i % 41) * 100_000m;
            double area = 14 + (i % 32);
            double distanceMeters = 100 + (i % 30) * 100;

            rooms.Add(CreateRoom(
                id: $"room-2026-{i:0000}",
                campusId: campusId,
                status: status,
                isVerified: isVerified,
                roomType: roomType,
                rent: rent,
                area: area,
                distanceMeters: distanceMeters));
        }

        return rooms;
    }

    private static ListingDocument CreateRoom(
        string id,
        string campusId,
        string status,
        bool isVerified,
        string roomType,
        decimal rent,
        double area,
        double distanceMeters,
        List<PhotoDocument>? photos = null)
    {
        return new ListingDocument
        {
            Id = id,
            CampusId = campusId,
            Title = $"Phòng {roomType} {id}",
            Description = $"Mô tả chi tiết cho {id}",
            Status = status,
            IsVerified = isVerified,
            ModerationStatus = isVerified ? "Approved" : "Pending",
            Rent = rent,
            Deposit = rent,
            Area = area,
            RoomType = roomType,
            Address = $"Khu vực {campusId}, Thái Nguyên",
            DistanceMeters = distanceMeters,
            Location = new GeoJsonPoint { Coordinates = [105.8066, 21.5850] },
            Amenities = ["Wifi", "PrivateWC"],
            SecurityScore = 4.2,
            Photos = photos ?? [new PhotoDocument { Url = $"https://cdn.example/{id}.jpg", Is360 = false, Order = 1 }],
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
