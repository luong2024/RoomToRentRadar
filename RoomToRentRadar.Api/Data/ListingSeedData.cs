using RoomToRentRadar.Api.Models;

namespace RoomToRentRadar.Api.Data;

public static class ListingSeedData
{
    public static IReadOnlyList<ListingDocument> Create()
    {
        var rules = new HouseRulesDocument
        {
            Version = 2,
            UpdatedAt = DateTimeOffset.Parse("2026-11-23T08:00:00Z"),
            QuietHours = "23:00 - 05:30",
            GuestPolicy = "Khách về trước 22:30, qua đêm báo trước chủ nhà",
            CookingRules = "Được nấu ăn bằng bếp từ, không dùng bếp gas mini",
            ParkingRules = "Để xe đúng vạch tầng 1, khóa cổ xe"
        };

        var rooms = new List<ListingDocument>
        {
            new()
            {
                Id = "room-2026-001",
                CampusId = "ictu-main",
                Title = "Studio Ban Công Thoáng Mát Cách Cổng Chính ICTU 400m",
                Description = "Phòng mới xây, an ninh vân tay, giờ giấc tự do, gần cổng Z115.",
                Status = "Available",
                IsVerified = true,
                Rent = 1_800_000m,
                Deposit = 1_800_000m,
                Area = 22.5,
                RoomType = "Studio",
                Address = "Ngõ 112 Đường Z115, Quyết Thắng, TP. Thái Nguyên",
                DistanceMeters = 400,
                Location = new GeoJsonPoint { Coordinates = [105.8066, 21.5850] },
                Amenities = ["Wifi", "WaterHeater", "AirConditioner", "PrivateWC", "FingerprintLock", "Balcony"],
                SecurityScore = 4.5,
                Photos =
                [
                    new PhotoDocument { Url = "https://cdn.example/p1.jpg", Is360 = false, Order = 1 },
                    new PhotoDocument { Url = "https://cdn.example/p360.jpg", Is360 = true, Order = 2 }
                ],
                HouseRules = rules,
                Landlord = new LandlordDocument { Id = "usr-landlord-09", Name = "Cô Nguyễn Thị Lan", Phone = "0987654321" },
                UpdatedAt = DateTimeOffset.Parse("2026-11-23T09:00:00Z")
            },
            new()
            {
                Id = "room-2026-002",
                CampusId = "ictu-main",
                Title = "Studio Full Nội Thất Cổng Phụ ICTU",
                Description = "Phòng sạch sẽ, full nội thất, phù hợp sinh viên năm nhất.",
                Status = "Available",
                IsVerified = true,
                Rent = 1_900_000m,
                Deposit = 1_900_000m,
                Area = 20,
                RoomType = "Studio",
                Address = "Ngõ 68 Đường Z115, Tân Thịnh, TP. Thái Nguyên",
                DistanceMeters = 520,
                Location = new GeoJsonPoint { Coordinates = [105.8080, 21.5865] },
                Amenities = ["Wifi", "AirConditioner", "PrivateWC"],
                SecurityScore = 4.0,
                Photos = [new PhotoDocument { Url = "https://cdn.example/p2.jpg", Is360 = false, Order = 1 }],
                HouseRules = rules,
                Landlord = new LandlordDocument { Id = "usr-landlord-10", Name = "Anh Minh", Phone = "0912345678" },
                UpdatedAt = DateTimeOffset.Parse("2026-11-22T10:00:00Z")
            },
            new()
            {
                Id = "room-2026-003",
                CampusId = "ictu-main",
                Title = "Phòng Khép Kín Gác Xép Gỗ Thoáng Mát",
                Description = "Khép kín, gác xép gỗ, có chỗ để xe máy.",
                Status = "Available",
                IsVerified = true,
                Rent = 1_650_000m,
                Deposit = 1_650_000m,
                Area = 24,
                RoomType = "Khép kín",
                Address = "Khu tập thể ĐH CNTT&TT, Quyết Thắng, TP. Thái Nguyên",
                DistanceMeters = 350,
                Location = new GeoJsonPoint { Coordinates = [105.8050, 21.5840] },
                Amenities = ["Wifi", "WaterHeater", "Parking"],
                SecurityScore = 3.8,
                Photos = [new PhotoDocument { Url = "https://cdn.example/p3.jpg", Is360 = false, Order = 1 }],
                HouseRules = rules,
                Landlord = new LandlordDocument { Id = "usr-landlord-11", Name = "Chị Hoa", Phone = "0909123456" },
                UpdatedAt = DateTimeOffset.Parse("2026-11-21T08:00:00Z")
            },
            new()
            {
                Id = "room-2026-004",
                CampusId = "ictu-main",
                Title = "Studio đã thuê — mẫu unavailable (US02 AC3)",
                Description = "Phòng đã có người thuê; dùng để test unavailable + similarRooms.",
                Status = "Rented",
                IsVerified = true,
                Rent = 1_750_000m,
                Deposit = 1_750_000m,
                Area = 21,
                RoomType = "Studio",
                Address = "Ngõ Z115, Quyết Thắng, TP. Thái Nguyên",
                DistanceMeters = 450,
                Location = new GeoJsonPoint { Coordinates = [105.8070, 21.5855] },
                Amenities = ["Wifi", "WaterHeater", "AirConditioner", "PrivateWC"],
                SecurityScore = 4.2,
                Photos = [new PhotoDocument { Url = "https://cdn.example/p4.jpg", Is360 = false, Order = 1 }],
                HouseRules = rules,
                Landlord = new LandlordDocument { Id = "usr-landlord-09", Name = "Cô Nguyễn Thị Lan", Phone = "0987654321" },
                UpdatedAt = DateTimeOffset.Parse("2026-11-20T12:00:00Z")
            },
            new()
            {
                Id = "room-2026-005",
                CampusId = "ictu-main",
                Title = "Studio cao cấp gần cổng chính ICTU",
                Description = "Full nội thất, ban công rộng.",
                Status = "Available",
                IsVerified = true,
                Rent = 2_500_000m,
                Deposit = 2_500_000m,
                Area = 30,
                RoomType = "Studio",
                Address = "Cổng chính ICTU, Quyết Thắng, TP. Thái Nguyên",
                DistanceMeters = 180,
                Location = new GeoJsonPoint { Coordinates = [105.8060, 21.5845] },
                Amenities = ["Wifi", "WaterHeater", "AirConditioner", "PrivateWC", "Balcony"],
                SecurityScore = 4.7,
                Photos = [new PhotoDocument { Url = "https://cdn.example/p5.jpg", Is360 = false, Order = 1 }],
                HouseRules = rules,
                Landlord = new LandlordDocument { Id = "usr-landlord-12", Name = "Chú Hùng", Phone = "0933456789" },
                UpdatedAt = DateTimeOffset.Parse("2026-11-19T09:00:00Z")
            }
        };

        rooms.AddRange(rooms.Take(5).Select((room, index) => CloneWithId(room, $"room-ictu-{(index + 1):000}")));
        return rooms;
    }

    private static ListingDocument CloneWithId(ListingDocument source, string id) => new()
    {
        Id = id,
        CampusId = source.CampusId,
        Title = source.Title,
        Description = source.Description,
        Status = source.Status,
        IsVerified = source.IsVerified,
        ModerationStatus = source.ModerationStatus,
        Rent = source.Rent,
        Deposit = source.Deposit,
        Area = source.Area,
        RoomType = source.RoomType,
        Address = source.Address,
        DistanceMeters = source.DistanceMeters,
        Location = new GeoJsonPoint
        {
            Type = source.Location.Type,
            Coordinates = [.. source.Location.Coordinates]
        },
        Amenities = [.. source.Amenities],
        SecurityScore = source.SecurityScore,
        Photos = source.Photos
            .Select(p => new PhotoDocument { Url = p.Url, Is360 = p.Is360, Order = p.Order })
            .ToList(),
        HouseRules = new HouseRulesDocument
        {
            Version = source.HouseRules.Version,
            UpdatedAt = source.HouseRules.UpdatedAt,
            QuietHours = source.HouseRules.QuietHours,
            GuestPolicy = source.HouseRules.GuestPolicy,
            CookingRules = source.HouseRules.CookingRules,
            ParkingRules = source.HouseRules.ParkingRules
        },
        Landlord = new LandlordDocument
        {
            Id = source.Landlord.Id,
            Name = source.Landlord.Name,
            Phone = source.Landlord.Phone
        },
        UpdatedAt = source.UpdatedAt
    };
}
