using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Outbound.Repositories;

[assembly: InternalsVisibleTo("RoomToRentRadarUnitTests")]

namespace RoomToRentRadar.Infrastructure.Adapters.Mocks;

public sealed class InMemoryRoomRepository : IRoomRepository
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryRoomRepository()
    {
        SeedDefaultRooms();
    }

    public Task<Room?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
        {
            return Task.FromResult<Room?>(null);
        }

        _rooms.TryGetValue(id.Trim(), out var room);
        return Task.FromResult(room);
    }

    public Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(
        string excludeRoomId,
        decimal targetPrice,
        double targetArea,
        double targetDistanceKm = 0.0,
        int limit = 3,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var safeLimit = Math.Max(1, limit);
        var similar = _rooms.Values
            .Where(r => !string.Equals(r.Id, excludeRoomId, StringComparison.OrdinalIgnoreCase) && r.IsAvailable)
            .OrderBy(r => Math.Abs(r.DistanceToCampusKm - targetDistanceKm))
            .ThenBy(r => Math.Abs(r.MonthlyRent - targetPrice))
            .ThenBy(r => Math.Abs(r.AreaSquareMeters - targetArea))
            .Take(safeLimit)
            .ToList();

        return Task.FromResult<IReadOnlyList<Room>>(similar);
    }

    internal void AddOrUpdate(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);
        _rooms[room.Id] = room;
    }

    private void SeedDefaultRooms()
    {
        var sampleRooms = new List<Room>
        {
            new(
                id: "room-ictu-001",
                title: "Phòng trọ khép kín cao cấp cổng chính ICTU",
                description: "Phòng trọ mới xây 100%, đầy đủ điều hòa, nóng lạnh, giường nệm, bàn học, ban công thoáng mát, an ninh camera 24/7.",
                monthlyRent: 1800000m,
                depositAmount: 1800000m,
                areaSquareMeters: 25.0,
                roomType: "Khép kín",
                amenities: new[] { "Điều hòa", "Nóng lạnh", "Wifi", "Giường nệm", "Tủ quần áo", "Ban công" },
                fullAddress: "Số 12 Đường Z115, Xã Quyết Thắng, TP Thái Nguyên (Cách cổng chính ICTU 200m)",
                landlordName: "Cô Lan (Chủ nhà)",
                landlordPhoneNumber: "0988123456",
                photoUrls: new[] { "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267", "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688" },
                panorama360Url: "https://my.matterport.com/show/?m=sample360room1",
                houseRules: new[] { "Không nuôi thú cưng", "Giữ gìn vệ sinh chung", "Khóa cổng sau 23h30" },
                status: ListingStatus.Available,
                lastUpdatedAt: new DateTime(2026, 11, 10, 8, 30, 0, DateTimeKind.Utc),
                isVerified: true,
                distanceToCampusKm: 0.2
            ),
            new(
                id: "room-ictu-002",
                title: "Chung cư mini sinh viên gần cổng phụ ĐH CNTT & TT",
                description: "Phòng rộng rãi cho 2 người ở, vệ sinh khép kín, có bếp nấu ăn riêng, giờ giấc tự do không chung chủ.",
                monthlyRent: 1500000m,
                depositAmount: 1000000m,
                areaSquareMeters: 22.0,
                roomType: "Chung cư mini",
                amenities: new[] { "Điều hòa", "Nóng lạnh", "Bếp riêng", "Wifi" },
                fullAddress: "Ngõ 45 Đường Z115, Xã Quyết Thắng, TP Thái Nguyên",
                landlordName: "Bác Hùng",
                landlordPhoneNumber: "0912345678",
                photoUrls: new[] { "https://images.unsplash.com/photo-1493809842364-78817add7ffb" },
                houseRules: new[] { "Giữ yên lặng sau 23h" },
                status: ListingStatus.Available,
                lastUpdatedAt: new DateTime(2026, 11, 9, 14, 0, 0, DateTimeKind.Utc),
                isVerified: true,
                distanceToCampusKm: 0.5
            ),
            new(
                id: "room-ictu-003",
                title: "Phòng trọ gác xép hiện đại đường Quang Trung",
                description: "Phòng đầy đủ nội thất, có gác lửng cao không đụng đầu, an ninh cực tốt.",
                monthlyRent: 1700000m,
                depositAmount: 1700000m,
                areaSquareMeters: 24.0,
                roomType: "Khép kín",
                amenities: new[] { "Điều hòa", "Gác lửng", "Nóng lạnh" },
                fullAddress: "Đường Quang Trung, Phường Thịnh Đán, TP Thái Nguyên",
                landlordName: "Chú Tuấn",
                landlordPhoneNumber: "0977888999",
                photoUrls: new[] { "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2" },
                houseRules: new[] { "Không hút thuốc trong phòng" },
                status: ListingStatus.Unpublished,
                lastUpdatedAt: new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc),
                isVerified: true,
                distanceToCampusKm: 1.2
            ),
            new(
                id: "room-ictu-004",
                title: "Phòng trọ giá sinh viên gần chợ Quyết Thắng",
                description: "Phòng thoáng mát, gần chợ, thuận tiện ăn uống sinh hoạt.",
                monthlyRent: 1200000m,
                depositAmount: 500000m,
                areaSquareMeters: 18.0,
                roomType: "Phòng trọ",
                amenities: new[] { "Quạt trần", "Nóng lạnh", "Wifi" },
                fullAddress: "Khu dân cư Chợ Quyết Thắng, TP Thái Nguyên",
                landlordName: "Cô Mai",
                landlordPhoneNumber: "0966554433",
                photoUrls: new[] { "https://images.unsplash.com/photo-1505691938895-1758d7feb511" },
                houseRules: new[] { "Đóng tiền nhà đúng ngày mùng 5" },
                status: ListingStatus.Rented,
                lastUpdatedAt: new DateTime(2026, 11, 8, 9, 0, 0, DateTimeKind.Utc),
                isVerified: false,
                distanceToCampusKm: 0.8
            )
        };

        foreach (var room in sampleRooms)
        {
            _rooms[room.Id] = room;
        }
    }
}
