using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services;

public interface IRoomListingService
{
    Task<ListingPage> GetPageAsync(Campus campus, int offset, int pageSize, CancellationToken cancellationToken = default);
}

public class RoomListingService(IEnumerable<RoomListing> listings) : IRoomListingService
{
    private readonly RoomListing[] _listings = listings.ToArray();

    public Task<ListingPage> GetPageAsync(Campus campus, int offset, int pageSize, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        cancellationToken.ThrowIfCancellationRequested();
        var sorted = _listings.Where(r => r.Status == ListingStatus.Available && r.IsVerified)
            .Select(r => new RoomListingItem(r, DistanceKm(campus.Latitude, campus.Longitude, r.Latitude, r.Longitude)))
            .OrderBy(r => r.DistanceKm).ThenBy(r => r.Room.Id).Skip(offset).Take(pageSize + 1).ToArray();
        return Task.FromResult(new ListingPage(sorted.Take(pageSize).ToArray(), sorted.Length > pageSize));
    }

    // Great-circle distance, not driving or walking distance.
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double radians = Math.PI / 180;
        var a = Math.Pow(Math.Sin((lat2 - lat1) * radians / 2), 2)
            + Math.Cos(lat1 * radians) * Math.Cos(lat2 * radians) * Math.Pow(Math.Sin((lon2 - lon1) * radians / 2), 2);
        return 6371 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }
}

public static class DemoRoomListings
{
    public static IReadOnlyList<Campus> Campuses { get; } =
    [
        new("bk", "ĐH Bách khoa — Lý Thường Kiệt", 10.7721, 106.6579),
        new("khtn", "ĐH Khoa học Tự nhiên — Nguyễn Văn Cừ", 10.7626, 106.6822)
    ];

    public static IEnumerable<RoomListing> Create() => Enumerable.Range(1, 500).Select(i =>
        new RoomListing(i, (i % 4) switch
        {
            0 => "Studio sáng thoáng, có ban công",
            1 => "Phòng ấm cúng, nhiều ánh sáng",
            2 => "Không gian riêng, đầy đủ tiện nghi",
            _ => "Phòng gác lửng, cửa sổ lớn"
        }, 2000000 + i % 12 * 250000, 18 + i % 25,
            10.75 + i % 25 * .002, 106.64 + i / 25 * .002,
            i % 7 == 0 ? ListingStatus.Rented : ListingStatus.Available, i % 5 != 0));
}
