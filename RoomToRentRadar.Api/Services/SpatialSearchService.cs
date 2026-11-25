using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services;

public interface ISpatialSearchService
{
    /// <summary>Trả về tin đăng có vị trí nằm trong polygon (GeoJSON ring [lng, lat]).</summary>
    Task<SpatialSearchResponseDto> SearchInPolygonAsync(
        SpatialSearchRequestDto request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US05 / T05.2 — lọc tin đăng theo vùng vẽ trên bản đồ.
/// Dùng ray-casting trên mặt phẳng lng/lat (đủ chính xác cho vùng vài km quanh campus).
/// Khi chuyển sang Cosmos DB có thể thay bằng ST_WITHIN trong repository.
/// </summary>
public sealed class SpatialSearchService(IListingRepository repository) : ISpatialSearchService
{
    public const int MinRingPoints = 4; // tam giác + điểm đóng
    public const int MaxRingPoints = 500; // giới hạn payload vẽ tay từ MAUI (T05.1)

    public async Task<SpatialSearchResponseDto> SearchInPolygonAsync(
        SpatialSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ring = Normalize(request.Polygon);
        var campusId = string.IsNullOrWhiteSpace(request.CampusId) ? "ictu-main" : request.CampusId;
        var listings = await repository.GetPublicListingsAsync(campusId, cancellationToken);

        var matched = listings.Where(l => IsInside(l.Location.Coordinates, ring));

        matched = (request.SortBy ?? "distance").Trim().ToLowerInvariant() switch
        {
            "rent" or "price" or "rent_asc" or "price_asc" => matched.OrderBy(l => l.Rent).ThenBy(l => l.DistanceMeters),
            "rent_desc" or "price_desc" => matched.OrderByDescending(l => l.Rent).ThenBy(l => l.DistanceMeters),
            "area" or "area_desc" => matched.OrderByDescending(l => l.Area).ThenBy(l => l.DistanceMeters),
            _ => matched.OrderBy(l => l.DistanceMeters)
        };

        var items = matched.Select(ToPin).ToList();
        return new SpatialSearchResponseDto { TotalCount = items.Count, Items = items };
    }

    /// <summary>Kiểm tra polygon hợp lệ; ném ArgumentException nếu không.</summary>
    public static IReadOnlyList<double[]> Normalize(IReadOnlyList<double[]>? polygon)
    {
        if (polygon is null || polygon.Count == 0)
        {
            throw new ArgumentException("Polygon is required.", nameof(polygon));
        }

        if (polygon.Count > MaxRingPoints)
        {
            throw new ArgumentException($"Polygon supports at most {MaxRingPoints} points.", nameof(polygon));
        }

        foreach (var point in polygon)
        {
            if (point is null || point.Length < 2
                || point[0] is < -180 or > 180
                || point[1] is < -90 or > 90)
            {
                throw new ArgumentException("Each point must be [longitude, latitude] within valid ranges.", nameof(polygon));
            }
        }

        var ring = polygon.ToList();
        var first = ring[0];
        var last = ring[^1];
        if (first[0] != last[0] || first[1] != last[1])
        {
            ring.Add([first[0], first[1]]); // tự đóng ring
        }

        var distinctPoints = ring.Take(ring.Count - 1).Select(p => (p[0], p[1])).Distinct().Count();
        if (ring.Count < MinRingPoints || distinctPoints < 3)
        {
            throw new ArgumentException("Polygon needs at least 3 distinct points.", nameof(polygon));
        }

        return ring;
    }

    public static bool IsInside(double[] point, IReadOnlyList<double[]> ring)
    {
        if (point.Length < 2)
        {
            return false;
        }

        double x = point[0], y = point[1];
        var inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            double xi = ring[i][0], yi = ring[i][1];
            double xj = ring[j][0], yj = ring[j][1];
            if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public static SpatialSearchPinDto ToPin(ListingDocument l) => new()
    {
        Id = l.Id,
        CampusId = l.CampusId,
        Title = l.Title,
        Rent = l.Rent,
        Area = l.Area,
        DistanceMeters = l.DistanceMeters,
        RoomType = l.RoomType,
        Address = l.Address,
        ThumbnailUrl = l.Photos.OrderBy(p => p.Order).Select(p => p.ThumbnailUrl is { Length: > 0 } t ? t : p.Url).FirstOrDefault() ?? string.Empty,
        IsVerified = l.IsVerified,
        Longitude = l.Location.Coordinates.ElementAtOrDefault(0),
        Latitude = l.Location.Coordinates.ElementAtOrDefault(1)
    };
}
