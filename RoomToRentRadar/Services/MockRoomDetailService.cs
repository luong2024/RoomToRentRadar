using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

/// <summary>
/// Dữ liệu mẫu phòng trọ chi tiết dùng khi Backend API T02.1 (http://localhost:5000) chưa chạy.
/// Đồng bộ ID với ListingSeedData (T02.1), T01.2 (room-ictu-*) và T05.1/T05.3 (room-map-*).
/// </summary>
public sealed class MockRoomDetailService : IRoomDetailService
{
    public const string DefaultUnavailableMessage =
        "Phòng này hiện không còn trống. Dưới đây là một số phòng tương tự đang Available.";

    private readonly IReadOnlyList<ListingDetailContract> _rooms;
    private readonly IReadOnlyDictionary<string, ListingDetailContract> _byId;

    public MockRoomDetailService()
    {
        _rooms = BuildSeedDetails();
        _byId = _rooms.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);
    }

    public Task<RoomDetailFetchResult> GetRoomDetailAsync(
        string roomId,
        bool isAuthenticated = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return Task.FromResult(new RoomDetailFetchResult(null, IsFromLiveApi: false));
        }

        var canonicalId = NormalizeToCanonicalId(roomId.Trim());
        if (!_byId.TryGetValue(canonicalId, out var source))
        {
            return Task.FromResult(new RoomDetailFetchResult(null, IsFromLiveApi: false));
        }

        var isAvailable = string.Equals(source.Status, "Available", StringComparison.OrdinalIgnoreCase);
        var similarRooms = isAvailable
            ? []
            : _rooms
                .Where(c => !string.Equals(c.Id, source.Id, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(c.Status, "Available", StringComparison.OrdinalIgnoreCase)
                            && c.IsVerified)
                .OrderBy(c => string.Equals(c.RoomType, source.RoomType, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(c => Math.Abs(c.Rent - source.Rent))
                .ThenBy(c => Math.Abs(c.Area - source.Area))
                .Take(3)
                .Select(ToCardDto)
                .ToList();

        var phone = isAuthenticated
            ? source.Landlord?.Phone ?? string.Empty
            : MaskPhone(source.Landlord?.Phone ?? string.Empty);

        var result = new ListingDetailContract
        {
            Id = roomId.Trim(),
            CampusId = source.CampusId,
            Title = source.Title,
            Description = source.Description,
            Status = source.Status,
            IsVerified = source.IsVerified,
            IsAvailable = isAvailable,
            UnavailableMessage = isAvailable ? null : DefaultUnavailableMessage,
            Rent = source.Rent,
            Deposit = source.Deposit,
            Area = source.Area,
            RoomType = source.RoomType,
            Address = source.Address,
            Location = source.Location is null
                ? null
                : new GeoJsonPointContract
                {
                    Type = source.Location.Type,
                    Coordinates = [.. source.Location.Coordinates]
                },
            Amenities = [.. source.Amenities],
            SecurityScore = source.SecurityScore,
            Photos = source.Photos
                .Select(p => new PhotoContract { Url = p.Url, Is360 = p.Is360, Order = p.Order })
                .ToList(),
            HouseRules = source.HouseRules is null
                ? null
                : new HouseRulesContract
                {
                    Version = source.HouseRules.Version,
                    UpdatedAt = source.HouseRules.UpdatedAt,
                    QuietHours = source.HouseRules.QuietHours,
                    GuestPolicy = source.HouseRules.GuestPolicy,
                    CookingRules = source.HouseRules.CookingRules,
                    ParkingRules = source.HouseRules.ParkingRules
                },
            Landlord = new LandlordContract
            {
                Id = source.Landlord?.Id ?? string.Empty,
                Name = source.Landlord?.Name ?? string.Empty,
                Phone = phone
            },
            UpdatedAt = source.UpdatedAt,
            SimilarRooms = similarRooms
        };

        return Task.FromResult(new RoomDetailFetchResult(result, IsFromLiveApi: false));
    }

    public static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return "*** (Sign in to view)";
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 3)
        {
            return "*** (Sign in to view)";
        }

        return $"{digits[..3]}*** (Sign in to view)";
    }

    private static string NormalizeToCanonicalId(string id)
    {
        if (id.StartsWith("room-ictu-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(id["room-ictu-".Length..], out var ictuNumber))
        {
            return $"room-2026-{ictuNumber:000}";
        }

        if (id.StartsWith("room-map-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(id["room-map-".Length..], out var mapNumber))
        {
            return $"room-2026-{mapNumber:000}";
        }

        return id;
    }

    private static ListingCardDto ToCardDto(ListingDetailContract source)
    {
        var distanceMeters = source.Location?.Coordinates is { Length: >= 2 } coords
            ? CalculateApproxDistanceMeters(coords[1], coords[0], 21.5850, 105.8066)
            : 400;

        return new ListingCardDto
        {
            Id = source.Id,
            CampusId = source.CampusId,
            Title = source.Title,
            Rent = source.Rent,
            Area = source.Area,
            DistanceMeters = distanceMeters,
            RoomType = source.RoomType,
            Address = source.Address,
            ThumbnailUrl = source.Photos.OrderBy(p => p.Order).Select(p => p.Url).FirstOrDefault() ?? string.Empty,
            Status = source.Status,
            IsVerified = source.IsVerified
        };
    }

    private static double CalculateApproxDistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double EarthRadiusMeters = 6_371_000.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLng = (lng2 - lng1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(EarthRadiusMeters * c);
    }

    private static IReadOnlyList<ListingDetailContract> BuildSeedDetails()
    {
        var sharedRules = new HouseRulesContract
        {
            Version = 2,
            UpdatedAt = DateTimeOffset.Parse("2026-11-23T08:00:00Z"),
            QuietHours = "23:00 - 05:30",
            GuestPolicy = "Khách về trước 22:30, qua đêm báo trước chủ nhà",
            CookingRules = "Được nấu ăn bằng bếp từ, không dùng bếp gas mini",
            ParkingRules = "Để xe đúng vạch tầng 1, khóa cổ xe"
        };

        return
        [
            new ListingDetailContract
            {
                Id = "room-2026-001",
                CampusId = "ictu-main",
                Title = "Studio Ban Công Thoáng Mát Cách Cổng Chính ICTU 400m",
                Description = "Phòng mới xây, an ninh vân tay, giờ giấc tự do, gần cổng Z115. Trang bị sẵn điều hòa Inverter tiết kiệm điện, bình nóng lạnh và ban công phơi đồ.",
                Status = "Available",
                IsVerified = true,
                IsAvailable = true,
                Rent = 1_800_000m,
                Deposit = 1_800_000m,
                Area = 22.5,
                RoomType = "Studio",
                Address = "Ngõ 112 Đường Z115, Quyết Thắng, TP. Thái Nguyên",
                Location = new GeoJsonPointContract { Coordinates = [105.8066, 21.5886] },
                Amenities = ["Wifi", "WaterHeater", "AirConditioner", "PrivateWC", "FingerprintLock", "Balcony"],
                SecurityScore = 4.8,
                Photos =
                [
                    new PhotoContract { Url = "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=900&q=80", Is360 = false, Order = 1 },
                    new PhotoContract { Url = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?auto=format&fit=crop&w=900&q=80", Is360 = false, Order = 2 },
                    new PhotoContract { Url = "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=900&q=80", Is360 = false, Order = 3 },
                    new PhotoContract { Url = "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=900&q=80", Is360 = true, Order = 4 }
                ],
                HouseRules = sharedRules,
                Landlord = new LandlordContract { Id = "usr-landlord-09", Name = "Cô Nguyễn Thị Lan", Phone = "0987654321" },
                UpdatedAt = DateTimeOffset.Parse("2026-11-23T09:00:00Z")
            },
            new ListingDetailContract
            {
                Id = "room-2026-002",
                CampusId = "ictu-main",
                Title = "Studio Full Nội Thất Cổng Phụ ICTU",
                Description = "Phòng sạch sẽ, full nội thất (tủ quần áo, bàn học, điều hòa, nóng lạnh), phù hợp sinh viên năm nhất ICTU.",
                Status = "Available",
                IsVerified = true,
                IsAvailable = true,
                Rent = 1_900_000m,
                Deposit = 1_900_000m,
                Area = 20.0,
                RoomType = "Studio",
                Address = "Ngõ 68 Đường Z115, Tân Thịnh, TP. Thái Nguyên",
                Location = new GeoJsonPointContract { Coordinates = [105.8066, 21.5897] },
                Amenities = ["Wifi", "AirConditioner", "PrivateWC", "WaterHeater"],
                SecurityScore = 4.9,
                Photos =
                [
                    new PhotoContract { Url = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?auto=format&fit=crop&w=900&q=80", Is360 = false, Order = 1 },
                    new PhotoContract { Url = "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=900&q=80", Is360 = true, Order = 2 }
                ],
                HouseRules = sharedRules,
                Landlord = new LandlordContract { Id = "usr-landlord-10", Name = "Anh Minh", Phone = "0912345678" },
                UpdatedAt = DateTimeOffset.Parse("2026-11-22T10:00:00Z")
            },
            new ListingDetailContract
            {
                Id = "room-2026-003",
                CampusId = "ictu-main",
                Title = "Phòng Khép Kín Gác Xép Gỗ Thoáng Mát",
                Description = "Phòng khép kín có gác xép gỗ chắc chắn, chỗ nấu ăn riêng và khu để xe rộng tầng 1.",
                Status = "Available",
                IsVerified = true,
                IsAvailable = true,
                Rent = 1_650_000m,
                Deposit = 1_650_000m,
                Area = 24.0,
                RoomType = "Khép kín",
                Address = "Khu tập thể ĐH CNTT&TT, Quyết Thắng, TP. Thái Nguyên",
                Location = new GeoJsonPointContract { Coordinates = [105.8066, 21.58815] },
                Amenities = ["Wifi", "WaterHeater", "Parking", "PrivateWC"],
                SecurityScore = 4.6,
                Photos =
                [
                    new PhotoContract { Url = "https://images.unsplash.com/photo-1598928506311-c55ded91a20c?auto=format&fit=crop&w=900&q=80", Is360 = false, Order = 1 }
                ],
                HouseRules = sharedRules,
                Landlord = new LandlordContract { Id = "usr-landlord-11", Name = "Chị Hoa", Phone = "0909123456" },
                UpdatedAt = DateTimeOffset.Parse("2026-11-21T08:00:00Z")
            },
            new ListingDetailContract
            {
                Id = "room-2026-004",
                CampusId = "ictu-main",
                Title = "Studio đã thuê — mẫu unavailable (US02 AC3)",
                Description = "Phòng này hiện đã có sinh viên đặt cọc thuê. Dùng để kiểm thử luồng hiển thị banner hết phòng và 3 phòng tương tự.",
                Status = "Rented",
                IsVerified = true,
                IsAvailable = false,
                UnavailableMessage = DefaultUnavailableMessage,
                Rent = 1_750_000m,
                Deposit = 1_750_000m,
                Area = 21.0,
                RoomType = "Studio",
                Address = "Ngõ Z115, Quyết Thắng, TP. Thái Nguyên",
                Location = new GeoJsonPointContract { Coordinates = [105.8066, 21.58905] },
                Amenities = ["Wifi", "WaterHeater", "AirConditioner", "PrivateWC"],
                SecurityScore = 4.5,
                Photos =
                [
                    new PhotoContract { Url = "https://images.unsplash.com/photo-1555854877-bab0e564b8d5?auto=format&fit=crop&w=900&q=80", Is360 = false, Order = 1 }
                ],
                HouseRules = sharedRules,
                Landlord = new LandlordContract { Id = "usr-landlord-09", Name = "Cô Nguyễn Thị Lan", Phone = "0987654321" },
                UpdatedAt = DateTimeOffset.Parse("2026-11-20T12:00:00Z")
            },
            new ListingDetailContract
            {
                Id = "room-2026-005",
                CampusId = "ictu-main",
                Title = "Studio cao cấp gần cổng chính ICTU",
                Description = "Chung cư mini cao cấp có thang máy, khóa vân tay, ban công rộng chỉ 180m tới cổng chính ICTU.",
                Status = "Available",
                IsVerified = true,
                IsAvailable = true,
                Rent = 2_500_000m,
                Deposit = 2_500_000m,
                Area = 30.0,
                RoomType = "Studio",
                Address = "Cổng chính ICTU, Quyết Thắng, TP. Thái Nguyên",
                Location = new GeoJsonPointContract { Coordinates = [105.8066, 21.58662] },
                Amenities = ["Wifi", "WaterHeater", "AirConditioner", "PrivateWC", "Balcony", "FingerprintLock"],
                SecurityScore = 4.9,
                Photos =
                [
                    new PhotoContract { Url = "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=900&q=80", Is360 = true, Order = 1 }
                ],
                HouseRules = sharedRules,
                Landlord = new LandlordContract { Id = "usr-landlord-12", Name = "Chú Hùng", Phone = "0933456789" },
                UpdatedAt = DateTimeOffset.Parse("2026-11-19T09:00:00Z")
            },
            new ListingDetailContract
            {
                Id = "room-2026-006",
                CampusId = "ictu-main",
                Title = "Phòng Khép Kín Ban Công Rộng Gần Hồ Điều Hòa",
                Description = "Phòng khép kín view thoáng gần hồ điều hòa, giờ giấc tự do, Wifi tốc độ cao miễn phí.",
                Status = "Available",
                IsVerified = true,
                IsAvailable = true,
                Rent = 1_650_000m,
                Deposit = 1_650_000m,
                Area = 21.0,
                RoomType = "Khép kín",
                Address = "Tổ 7 Phường Tân Thịnh, TP. Thái Nguyên",
                Location = new GeoJsonPointContract { Coordinates = [105.8066, 21.5958] },
                Amenities = ["Wifi", "PrivateWC", "Balcony", "Parking"],
                SecurityScore = 4.7,
                Photos =
                [
                    new PhotoContract { Url = "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=900&q=80", Is360 = false, Order = 1 }
                ],
                HouseRules = sharedRules,
                Landlord = new LandlordContract { Id = "usr-landlord-13", Name = "Cô Hạnh", Phone = "0977112233" },
                UpdatedAt = DateTimeOffset.Parse("2026-11-18T09:00:00Z")
            },
            new ListingDetailContract
            {
                Id = "room-2026-007",
                CampusId = "ictu-main",
                Title = "Phòng Gác Xép Có Điều Hòa Cách ICTU 1.5km",
                Description = "Phòng gác xép thoáng mát trang bị sẵn điều hòa Inverter, phù hợp ở 1-2 sinh viên.",
                Status = "Available",
                IsVerified = true,
                IsAvailable = true,
                Rent = 1_350_000m,
                Deposit = 1_350_000m,
                Area = 18.5,
                RoomType = "Gác xép",
                Address = "Phường Quyết Thắng, TP. Thái Nguyên",
                Location = new GeoJsonPointContract { Coordinates = [105.8066, 21.5985] },
                Amenities = ["Wifi", "AirConditioner", "Parking"],
                SecurityScore = 4.4,
                Photos =
                [
                    new PhotoContract { Url = "https://images.unsplash.com/photo-1616594039964-ae9021a400a0?auto=format&fit=crop&w=900&q=80", Is360 = false, Order = 1 }
                ],
                HouseRules = sharedRules,
                Landlord = new LandlordContract { Id = "usr-landlord-14", Name = "Bác Tuấn", Phone = "0966554433" },
                UpdatedAt = DateTimeOffset.Parse("2026-11-17T09:00:00Z")
            },
            new ListingDetailContract
            {
                Id = "room-2026-008",
                CampusId = "ictu-main",
                Title = "Chung Cư Mini 2 Giường Cho Nhóm Sinh Viên Ở Ghép",
                Description = "Căn hộ mini 35m² với 2 giường tách biệt, máy giặt chung, tủ lạnh và ban công lớn.",
                Status = "Available",
                IsVerified = true,
                IsAvailable = true,
                Rent = 3_200_000m,
                Deposit = 3_200_000m,
                Area = 35.0,
                RoomType = "Chung cư mini",
                Address = "Đường Lương Ngọc Quyến, TP. Thái Nguyên",
                Location = new GeoJsonPointContract { Coordinates = [105.8066, 21.6012] },
                Amenities = ["Wifi", "WaterHeater", "AirConditioner", "PrivateWC", "Balcony", "FingerprintLock"],
                SecurityScore = 4.8,
                Photos =
                [
                    new PhotoContract { Url = "https://images.unsplash.com/photo-1595526114035-0d45ed16cfbf?auto=format&fit=crop&w=900&q=80", Is360 = true, Order = 1 }
                ],
                HouseRules = sharedRules,
                Landlord = new LandlordContract { Id = "usr-landlord-15", Name = "Anh Nam", Phone = "0944889900" },
                UpdatedAt = DateTimeOffset.Parse("2026-11-16T09:00:00Z")
            },
            new ListingDetailContract
            {
                Id = "room-2026-009",
                CampusId = "ictu-main",
                Title = "Phòng Khép Kín Sinh Viên Giá Rẻ Gần Chợ Z115",
                Description = "Phòng trọ sinh viên giá tiết kiệm cách chợ Z115 vài bước chân, điện nước tính theo giá nhà nước.",
                Status = "Available",
                IsVerified = true,
                IsAvailable = true,
                Rent = 1_200_000m,
                Deposit = 1_200_000m,
                Area = 16.0,
                RoomType = "Khép kín",
                Address = "Tổ 5 Phường Tân Thịnh, TP. Thái Nguyên",
                Location = new GeoJsonPointContract { Coordinates = [105.8066, 21.5913] },
                Amenities = ["Wifi", "WaterHeater", "PrivateWC"],
                SecurityScore = 4.5,
                Photos =
                [
                    new PhotoContract { Url = "https://images.unsplash.com/photo-1555854877-bab0e564b8d5?auto=format&fit=crop&w=900&q=80", Is360 = false, Order = 1 }
                ],
                HouseRules = sharedRules,
                Landlord = new LandlordContract { Id = "usr-landlord-16", Name = "Cô Mai", Phone = "0988776655" },
                UpdatedAt = DateTimeOffset.Parse("2026-11-15T09:00:00Z")
            }
        ];
    }
}
