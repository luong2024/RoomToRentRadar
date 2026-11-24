namespace RoomToRentRadar.Domain.ValueObjects;

using RoomToRentRadar.Domain.Exceptions;

/// <summary>
/// Immutable value object representing a closed search outline and its valid geographic rings.
/// The search rings use counter-clockwise winding for GeoJSON spatial queries.
/// </summary>
public sealed class GeoPolygon : IEquatable<GeoPolygon>
{
    public const double MinLatitude = -85.0511;
    public const double MaxLatitude = 85.0511;
    public const double MinLongitude = -180.0;
    public const double MaxLongitude = 180.0;
    public const double MinimumAreaSquareMeters = 400.0;
    private const double ProximityTolerance = 1e-6;

    /// <summary>
    /// Closed outline of the selection. It may self-intersect and is not a spatial query ring.
    /// Simple outlines are normalized counter-clockwise.
    /// </summary>
    public IReadOnlyList<GeoLocation> RawOutline { get; }

    /// <summary>
    /// Outline vertices without the closing point. A shared junction may be repeated.
    /// </summary>
    public IReadOnlyList<GeoLocation> DistinctVertices { get; }

    /// <summary>
    /// The geodesic surface area enclosed by this polygon in square meters (mÂ²).
    /// </summary>
    public double AreaSquareMeters { get; }

    /// <summary>
    /// Closed, counter-clockwise rings for spatial queries against the search outline.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<GeoLocation>> SearchRings { get; }

    /// <summary>Whether the second search ring is a hole in the first ring.</summary>
    public bool HasHole { get; }

    public GeoPolygon(IEnumerable<GeoLocation> coordinates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);

        var rawList = coordinates.ToList();
        if (rawList.Count < 3)
        {
            throw new InvalidPolygonException("A polygon requires at least 3 distinct coordinates.");
        }

        // Validate each coordinate against EPSG:3857 / Cosmos DB spatial bounds
        foreach (var coord in rawList)
        {
            ValidateCoordinateBounds(coord.Latitude, coord.Longitude);
        }

        // Extract distinct consecutive vertices and strip closing duplicate if present
        var distinct = NormalizePolygonPoints(rawList);
        if (distinct.Count < 3)
        {
            throw new InvalidPolygonException("A polygon requires at least 3 distinct non-collinear coordinates.");
        }

        if (IsSelfIntersecting(distinct))
        {
            if (!TrySplitLoops(distinct, out var first, out var second))
                throw new InvalidPolygonException("The search outline must enclose one area or cross at one junction.");

            var (firstArea, firstIsCcw) = CalculateSignedMetricArea(first);
            var (secondArea, secondIsCcw) = CalculateSignedMetricArea(second);
            bool secondIsInsideFirst = ContainsPoint(first, second[1]);
            bool firstIsInsideSecond = ContainsPoint(second, first[1]);
            HasHole = secondIsInsideFirst || firstIsInsideSecond;
            AreaSquareMeters = HasHole ? Math.Abs(firstArea - secondArea) : firstArea + secondArea;
            if (firstArea <= 0 || secondArea <= 0)
                throw new InvalidPolygonException("Each part of a crossed outline must enclose an area.");

            if (!firstIsCcw) first.Reverse();
            if (!secondIsCcw) second.Reverse();
            var firstRing = CloseRing(first);
            var secondRing = CloseRing(second);
            SearchRings = firstIsInsideSecond
                ? Array.AsReadOnly(new[] { secondRing, firstRing })
                : Array.AsReadOnly(new[] { firstRing, secondRing });
        }
        else
        {
            var (area, isCcw) = CalculateSignedMetricArea(distinct);
            AreaSquareMeters = area;
            if (!isCcw) distinct.Reverse();
            SearchRings = Array.AsReadOnly(new[] { CloseRing(distinct) });
        }

        if (AreaSquareMeters < MinimumAreaSquareMeters)
            throw new InvalidPolygonException(
                $"Polygon area is too small ({AreaSquareMeters:F1} m²). Minimum required area is {MinimumAreaSquareMeters} m².");

        DistinctVertices = distinct.AsReadOnly();
        RawOutline = SearchRings.Count == 1 ? SearchRings[0] : CloseRing(distinct);
    }

    private static IReadOnlyList<GeoLocation> CloseRing(List<GeoLocation> vertices)
    {
        var closed = new List<GeoLocation>(vertices) { vertices[0] };
        return closed.AsReadOnly();
    }

    private static void ValidateCoordinateBounds(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude))
        {
            throw new InvalidPolygonException("Polygon coordinates must be finite real numbers.");
        }

        if (latitude < MinLatitude || latitude > MaxLatitude)
        {
            throw new InvalidPolygonException(
                $"Latitude ({latitude:F4}) is outside the valid Web Mercator / Cosmos DB boundary [{MinLatitude}, {MaxLatitude}].");
        }

        if (longitude < MinLongitude || longitude > MaxLongitude)
        {
            throw new InvalidPolygonException(
                $"Longitude ({longitude:F4}) is outside the valid boundary [{MinLongitude}, {MaxLongitude}].");
        }
    }

    private static List<GeoLocation> NormalizePolygonPoints(List<GeoLocation> rawList)
    {
        var vertices = new List<GeoLocation>();
        foreach (var coordinate in rawList)
        {
            if (vertices.Count == 0 || !IsSameLocation(vertices[^1], coordinate))
                vertices.Add(coordinate);
        }
        if (vertices.Count > 1 && IsSameLocation(vertices[0], vertices[^1]))
            vertices.RemoveAt(vertices.Count - 1);
        return vertices;
    }

    private static bool IsSameLocation(GeoLocation a, GeoLocation b)
    {
        return Math.Abs(a.Latitude - b.Latitude) <= ProximityTolerance &&
               Math.Abs(a.Longitude - b.Longitude) <= ProximityTolerance;
    }

    /// <summary>
    /// Calculates the enclosed metric area (mÂ²) and determines if the points are CCW.
    /// Uses equidistant tangent plane projection centered at the first vertex.
    /// </summary>
    private static (double AreaM2, bool IsCcw) CalculateSignedMetricArea(IReadOnlyList<GeoLocation> points)
    {
        double originLat = points[0].Latitude;
        double originLon = points[0].Longitude;
        double originLatRad = originLat * Math.PI / 180.0;

        const double earthRadiusMeters = 6371000.0;
        double metersPerDegreeLat = (Math.PI / 180.0) * earthRadiusMeters;
        double metersPerDegreeLon = (Math.PI / 180.0) * earthRadiusMeters * Math.Cos(originLatRad);

        double signedSum = 0.0;
        int n = points.Count;

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;

            double xi = (points[i].Longitude - originLon) * metersPerDegreeLon;
            double yi = (points[i].Latitude - originLat) * metersPerDegreeLat;

            double xNext = (points[next].Longitude - originLon) * metersPerDegreeLon;
            double yNext = (points[next].Latitude - originLat) * metersPerDegreeLat;

            signedSum += (xi * yNext) - (xNext * yi);
        }

        double areaM2 = Math.Abs(signedSum) * 0.5;
        // In standard Cartesian coordinates (X = Lon, Y = Lat), positive signed area is CCW
        bool isCcw = signedSum > 0.0;

        return (areaM2, isCcw);
    }

    private static bool ContainsPoint(IReadOnlyList<GeoLocation> ring, GeoLocation point)
    {
        bool inside = false;
        for (int i = 0; i < ring.Count; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Count];
            if ((a.Latitude > point.Latitude) == (b.Latitude > point.Latitude))
                continue;

            double intersection = a.Longitude + (point.Latitude - a.Latitude) *
                (b.Longitude - a.Longitude) / (b.Latitude - a.Latitude);
            if (point.Longitude < intersection)
                inside = !inside;
        }
        return inside;
    }

    private static bool TrySplitLoops(List<GeoLocation> path, out List<GeoLocation> first,
        out List<GeoLocation> second)
    {
        first = new();
        second = new();
        for (int i = 0; i < path.Count; i++)
        {
            var a = path[i];
            var b = path[(i + 1) % path.Count];
            for (int j = i + 2; j < path.Count; j++)
            {
                if (i == 0 && j == path.Count - 1) continue;
                var c = path[j];
                var d = path[(j + 1) % path.Count];
                if (!DoSegmentsIntersect(a, b, c, d)) continue;

                double dx = b.Longitude - a.Longitude, dy = b.Latitude - a.Latitude;
                double ex = d.Longitude - c.Longitude, ey = d.Latitude - c.Latitude;
                double denominator = dx * ey - dy * ex;
                if (denominator == 0) continue;
                double t = ((c.Longitude - a.Longitude) * ey - (c.Latitude - a.Latitude) * ex) / denominator;
                var cross = new GeoLocation(a.Latitude + t * dy, a.Longitude + t * dx);
                first = NormalizePolygonPoints(path.GetRange(i + 1, j - i).Prepend(cross).ToList());
                second = NormalizePolygonPoints(path.Skip(j + 1).Concat(path.Take(i + 1)).Prepend(cross).ToList());
                return first.Count >= 3 && second.Count >= 3 &&
                    !IsSelfIntersecting(first) && !IsSelfIntersecting(second) &&
                    !LoopsCrossAwayFromJunction(first, second, cross);
            }
        }
        return false;
    }

    private static bool LoopsCrossAwayFromJunction(List<GeoLocation> first,
        List<GeoLocation> second, GeoLocation junction)
    {
        for (int i = 0; i < first.Count; i++)
        {
            var a = first[i];
            var b = first[(i + 1) % first.Count];
            for (int j = 0; j < second.Count; j++)
            {
                var c = second[j];
                var d = second[(j + 1) % second.Count];
                if (!DoSegmentsIntersect(a, b, c, d))
                    continue;

                if ((a != junction && b != junction) || (c != junction && d != junction))
                    return true;

                var firstEnd = a == junction ? b : a;
                var secondEnd = c == junction ? d : c;
                if (GetOrientation(junction, firstEnd, secondEnd) == 0 &&
                    (IsPointOnSegment(junction, firstEnd, secondEnd) ||
                     IsPointOnSegment(junction, secondEnd, firstEnd)))
                    return true;
            }
        }
        return false;
    }

    private static bool IsSelfIntersecting(IReadOnlyList<GeoLocation> vertices)
    {
        if (vertices == null || vertices.Count < 4)
            return false; // A triangle cannot self-intersect

        int n = vertices.Count;
        for (int i = 0; i < n; i++)
        {
            var a1 = vertices[i];
            var a2 = vertices[(i + 1) % n];

            for (int j = i + 1; j < n; j++)
            {
                // Adjacent edges share an endpoint, skip them
                if (Math.Abs(i - j) <= 1 || (i == 0 && j == n - 1))
                    continue;

                var b1 = vertices[j];
                var b2 = vertices[(j + 1) % n];

                if (DoSegmentsIntersect(a1, a2, b1, b2))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool DoSegmentsIntersect(GeoLocation p1, GeoLocation q1, GeoLocation p2, GeoLocation q2)
    {
        if (Math.Max(p1.Longitude, q1.Longitude) < Math.Min(p2.Longitude, q2.Longitude) ||
            Math.Max(p2.Longitude, q2.Longitude) < Math.Min(p1.Longitude, q1.Longitude) ||
            Math.Max(p1.Latitude, q1.Latitude) < Math.Min(p2.Latitude, q2.Latitude) ||
            Math.Max(p2.Latitude, q2.Latitude) < Math.Min(p1.Latitude, q1.Latitude))
            return false;

        int o1 = GetOrientation(p1, q1, p2);
        int o2 = GetOrientation(p1, q1, q2);
        int o3 = GetOrientation(p2, q2, p1);
        int o4 = GetOrientation(p2, q2, q1);

        // General case: segments cross each other
        if (o1 * o2 < 0 && o3 * o4 < 0)
            return true;

        // Special collinear cases:
        if (o1 == 0 && IsPointOnSegment(p1, p2, q1)) return true;
        if (o2 == 0 && IsPointOnSegment(p1, q2, q1)) return true;
        if (o3 == 0 && IsPointOnSegment(p2, p1, q2)) return true;
        if (o4 == 0 && IsPointOnSegment(p2, q1, q2)) return true;

        return false;
    }

    private static int GetOrientation(GeoLocation p, GeoLocation q, GeoLocation r)
    {
        double val = (q.Longitude - p.Longitude) * (r.Latitude - p.Latitude) -
                     (q.Latitude - p.Latitude) * (r.Longitude - p.Longitude);

        return Math.Sign(val);
    }

    private static bool IsPointOnSegment(GeoLocation p, GeoLocation q, GeoLocation r)
    {
        return q.Longitude <= Math.Max(p.Longitude, r.Longitude) &&
               q.Longitude >= Math.Min(p.Longitude, r.Longitude) &&
               q.Latitude <= Math.Max(p.Latitude, r.Latitude) &&
               q.Latitude >= Math.Min(p.Latitude, r.Latitude);
    }

    public bool Equals(GeoPolygon? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (RawOutline.Count != other.RawOutline.Count) return false;

        for (int i = 0; i < RawOutline.Count; i++)
        {
            if (!IsSameLocation(RawOutline[i], other.RawOutline[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as GeoPolygon);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var v in DistinctVertices)
        {
            hash.Add(Math.Round(v.Latitude, 5));
            hash.Add(Math.Round(v.Longitude, 5));
        }
        return hash.ToHashCode();
    }
}
