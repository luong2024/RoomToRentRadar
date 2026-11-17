using System.Collections.Concurrent;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.InMemory;

public class InMemoryRoomRepository : IRoomRepository
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryRoomRepository()
    {
    }

    public InMemoryRoomRepository(IEnumerable<Room> initialRooms)
    {
        foreach (var room in initialRooms)
        {
            _rooms[room.Id] = room;
        }
    }

    public void Upsert(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);
        _rooms[room.Id] = room;
    }

    public Task<Room?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Task.FromResult<Room?>(null);
        }

        _rooms.TryGetValue(id, out var room);
        return Task.FromResult(room);
    }

    public Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(string roomId, decimal price, double area, CancellationToken cancellationToken = default)
    {
        var minPrice = price * 0.7m;
        var maxPrice = price * 1.3m;
        var minArea = area * 0.7;
        var maxArea = area * 1.3;

        var similar = _rooms.Values
            .Where(r => !string.Equals(r.Id, roomId, StringComparison.OrdinalIgnoreCase) &&
                        r.Status == RoomStatus.Available &&
                        r.Price >= minPrice && r.Price <= maxPrice &&
                        r.Area >= minArea && r.Area <= maxArea)
            .Take(5)
            .ToList();

        return Task.FromResult<IReadOnlyList<Room>>(similar);
    }

    public Task<(IReadOnlyList<Room> Items, int TotalCount)> SearchAsync(
        SearchRoomsQuery query,
        CancellationToken ct = default)
    {
        // 1. Lọc Available và Verified (AC4)
        var filtered = _rooms.Values.Where(r => r.Status == RoomStatus.Available && r.IsVerified);

        // 2. Lọc Giá (Price)
        if (query.MinPrice.HasValue)
        {
            filtered = filtered.Where(r => r.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            filtered = filtered.Where(r => r.Price <= query.MaxPrice.Value);
        }

        // 3. Lọc Diện tích (Area)
        if (query.MinArea.HasValue)
        {
            filtered = filtered.Where(r => r.Area >= query.MinArea.Value);
        }

        if (query.MaxArea.HasValue)
        {
            filtered = filtered.Where(r => r.Area <= query.MaxArea.Value);
        }

        // 4. Lọc Loại phòng (RoomType)
        if (!string.IsNullOrWhiteSpace(query.RoomType))
        {
            filtered = filtered.Where(r => string.Equals(r.RoomType, query.RoomType, StringComparison.OrdinalIgnoreCase));
        }

        // 5. Lọc Từ khóa (Keyword trong Title, Description, Address)
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            filtered = filtered.Where(r =>
                (r.Title != null && r.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                (r.Description != null && r.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                (r.Address != null && r.Address.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }

        // 6. Lọc Bán kính (RadiusInKm) theo công thức Haversine
        if (query.Latitude.HasValue && query.Longitude.HasValue && query.RadiusInKm.HasValue)
        {
            var userLat = query.Latitude.Value;
            var userLon = query.Longitude.Value;
            var radius = query.RadiusInKm.Value;

            filtered = filtered.Where(r =>
                r.Latitude.HasValue &&
                r.Longitude.HasValue &&
                CalculateHaversineDistanceKm(userLat, userLon, r.Latitude.Value, r.Longitude.Value) <= radius);
        }

        var list = filtered.OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
        var totalCount = list.Count;

        // 7. Phân trang (Pagination)
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 10 : query.PageSize, 1, 50);
        long rawOffset = ((long)pageNumber - 1) * pageSize;

        var pagedItems = rawOffset >= totalCount || rawOffset > int.MaxValue
            ? new List<Room>()
            : list.Skip((int)rawOffset).Take(pageSize).ToList();

        return Task.FromResult<(IReadOnlyList<Room> Items, int TotalCount)>((pagedItems, totalCount));
    }

    private static double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusKm = 6371.0;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var rLat1 = DegreesToRadians(lat1);
        var rLat2 = DegreesToRadians(lat2);

        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0) * Math.Cos(rLat1) * Math.Cos(rLat2);
        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

        return EarthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
