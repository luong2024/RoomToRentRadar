namespace RoomToRentRadar.Api.Services.Geo;

/// <summary>
/// Polygon GeoJSON ring (mỗi điểm [longitude, latitude]) — validate, tự đóng ring và kiểm tra điểm nằm trong.
/// Dùng chung cho tìm theo vùng vẽ (T05.2) và tìm kiếm đã lưu (T07.1).
/// Ray-casting trên mặt phẳng lng/lat đủ chính xác cho vùng vài km quanh campus.
/// </summary>
public static class GeoPolygon
{
    public const string FieldName = "polygon";
    public const int MinRingPoints = 4; // tam giác + điểm đóng
    public const int MaxRingPoints = 500; // giới hạn payload vẽ tay từ MAUI (T05.1)
    private const int MinDistinctPoints = 3;

    /// <summary>Validate polygon; trả <c>false</c> kèm thông báo lỗi tiếng Việt nếu không hợp lệ.</summary>
    public static bool TryNormalize(
        IReadOnlyList<double[]>? polygon,
        out IReadOnlyList<double[]> ring,
        out string error)
    {
        ring = [];
        error = string.Empty;

        if (polygon is null || polygon.Count == 0)
        {
            error = "Vui lòng vẽ vùng tìm kiếm.";
            return false;
        }

        if (polygon.Count > MaxRingPoints)
        {
            error = $"Vùng vẽ tối đa {MaxRingPoints} điểm.";
            return false;
        }

        if (!polygon.All(IsValidCoordinate))
        {
            error = "Mỗi điểm phải là [kinh độ, vĩ độ] hợp lệ.";
            return false;
        }

        var closed = CloseRing(polygon);
        if (closed.Count < MinRingPoints || CountDistinctPoints(closed) < MinDistinctPoints)
        {
            error = $"Vùng vẽ cần ít nhất {MinDistinctPoints} điểm khác nhau.";
            return false;
        }

        ring = closed;
        return true;
    }

    /// <summary>Như <see cref="TryNormalize"/> nhưng ném <see cref="RequestValidationException"/> (→ HTTP 400).</summary>
    public static IReadOnlyList<double[]> Normalize(IReadOnlyList<double[]>? polygon) =>
        TryNormalize(polygon, out var ring, out var error)
            ? ring
            : throw RequestValidationException.For(FieldName, error);

    /// <summary>Điểm [lng, lat] có nằm trong ring đã chuẩn hóa không.</summary>
    public static bool Contains(IReadOnlyList<double[]> ring, double[] point)
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

    private static bool IsValidCoordinate(double[]? point) =>
        point is { Length: >= 2 }
        && point[0] is >= -180 and <= 180
        && point[1] is >= -90 and <= 90;

    private static List<double[]> CloseRing(IReadOnlyList<double[]> polygon)
    {
        var ring = polygon.ToList();
        var first = ring[0];
        var last = ring[^1];
        if (first[0] != last[0] || first[1] != last[1])
        {
            ring.Add([first[0], first[1]]);
        }

        return ring;
    }

    private static int CountDistinctPoints(IReadOnlyList<double[]> closedRing) =>
        closedRing.Take(closedRing.Count - 1).Select(p => (p[0], p[1])).Distinct().Count();
}
