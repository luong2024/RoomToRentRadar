namespace RoomToRentRadar.Domain.ValueObjects;

using RoomToRentRadar.Domain.Exceptions;

/// <summary>
/// Immutable value object representing a validated, closed geographic polygon ring.
/// Vertices are normalized to Counter-Clockwise (CCW) winding order per GeoJSON RFC 7946
/// and Azure Cosmos DB spatial standards.
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
    /// Closed ring vertices where the first and last vertex are identical (V_0 == V_n).
    /// Ordered counter-clockwise (CCW).
    /// </summary>
    public IReadOnlyList<GeoLocation> Vertices { get; }

    /// <summary>
    /// Distinct non-closing vertices (V_0 through V_{n-1}).
    /// </summary>
    public IReadOnlyList<GeoLocation> DistinctVertices { get; }

    /// <summary>
    /// The geodesic surface area enclosed by this polygon in square meters (m²).
    /// </summary>
    public double AreaSquareMeters { get; }

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
        var distinct = ExtractDistinctVertices(rawList);
        if (distinct.Count < 3)
        {
            throw new InvalidPolygonException("A polygon requires at least 3 distinct non-collinear coordinates.");
        }

        // Calculate localized metric signed area and verify non-degeneracy
        var (metricAreaM2, isCcw) = CalculateSignedMetricArea(distinct);
        if (metricAreaM2 < MinimumAreaSquareMeters)
        {
            throw new InvalidPolygonException(
                $"Polygon area is too small ({metricAreaM2:F1} m²). Minimum required area is {MinimumAreaSquareMeters} m².");
        }

        AreaSquareMeters = metricAreaM2;

        // Normalize exterior ring to Counter-Clockwise (CCW) order
        if (!isCcw)
        {
            distinct.Reverse();
        }

        DistinctVertices = distinct.AsReadOnly();

        // Form closed ring: V_0, V_1, ..., V_{n-1}, V_0
        var closedRing = new List<GeoLocation>(distinct.Count + 1);
        closedRing.AddRange(distinct);
        closedRing.Add(distinct[0]);
        Vertices = closedRing.AsReadOnly();
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

    private static List<GeoLocation> ExtractDistinctVertices(List<GeoLocation> rawList)
    {
        var distinct = new List<GeoLocation>();
        foreach (var coord in rawList)
        {
            if (!distinct.Any(existing => IsSameLocation(existing, coord)))
            {
                distinct.Add(coord);
            }
        }

        return distinct;
    }

    private static bool IsSameLocation(GeoLocation a, GeoLocation b)
    {
        return Math.Abs(a.Latitude - b.Latitude) <= ProximityTolerance &&
               Math.Abs(a.Longitude - b.Longitude) <= ProximityTolerance;
    }

    /// <summary>
    /// Calculates the enclosed metric area (m²) and determines if the points are CCW.
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

    public bool Equals(GeoPolygon? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Vertices.Count != other.Vertices.Count) return false;

        for (int i = 0; i < Vertices.Count; i++)
        {
            if (!IsSameLocation(Vertices[i], other.Vertices[i]))
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
