using System.Globalization;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

/// <summary>
/// A rejected drawn search area. Returned instead of an exception because an
/// invalid polygon is an expected client error (HTTP 422).
/// </summary>
public sealed record AreaValidationError(string Code, string Field, string Detail);

/// <summary>
/// The validated and prepared polygon behind an area search. Geometry is built
/// once per request; the prepared predicate keeps point classification cheap for
/// every candidate row.
/// </summary>
public sealed class SearchArea
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    private readonly IPreparedGeometry _prepared;

    internal SearchArea(Polygon polygon, IPreparedGeometry prepared, double approximateAreaKm2)
    {
        Polygon = polygon;
        _prepared = prepared;
        ApproximateAreaKm2 = approximateAreaKm2;

        var envelope = polygon.EnvelopeInternal;
        MinLng = envelope.MinX;
        MaxLng = envelope.MaxX;
        MinLat = envelope.MinY;
        MaxLat = envelope.MaxY;

        var centroid = polygon.Centroid;
        CentroidLng = centroid.X;
        CentroidLat = centroid.Y;
    }

    public Polygon Polygon { get; }

    /// <summary>Inclusive longitude bound of the prepared polygon.</summary>
    public double MinLng { get; }

    /// <summary>Inclusive longitude bound of the prepared polygon.</summary>
    public double MaxLng { get; }

    /// <summary>Inclusive latitude bound of the prepared polygon.</summary>
    public double MinLat { get; }

    /// <summary>Inclusive latitude bound of the prepared polygon.</summary>
    public double MaxLat { get; }

    /// <summary>Default distance reference: the polygon centroid longitude.</summary>
    public double CentroidLng { get; }

    /// <summary>Default distance reference: the polygon centroid latitude.</summary>
    public double CentroidLat { get; }

    /// <summary>
    /// Area in square kilometres from a local equirectangular projection scaled by
    /// the cosine of the mean latitude. Accurate enough at city scale, not a
    /// geodesic area.
    /// </summary>
    public double ApproximateAreaKm2 { get; }

    /// <summary>
    /// True when the point lies inside the polygon or on its boundary. Vertices and
    /// ring positions are therefore included.
    /// </summary>
    public bool Intersects(double lng, double lat)
        => _prepared.Intersects(Factory.CreatePoint(new Coordinate(lng, lat)));
}

/// <summary>
/// Pure, EF-free parsing and validation of a drawn GeoJSON polygon. Candidates are
/// selected with the bounding box and then classified with the exact prepared
/// geometry predicate.
/// </summary>
public static class GeoSearch
{
    public const string InvalidAreaType = "invalid_area_type";
    public const string InvalidAreaRing = "invalid_area_ring";
    public const string InvalidAreaVertexCount = "invalid_area_vertex_count";
    public const string InvalidAreaCoordinate = "invalid_area_coordinate";
    public const string UnsupportedAreaHoles = "unsupported_area_holes";
    public const string SelfIntersectingArea = "self_intersecting_area";
    public const string AreaTooLarge = "area_too_large";
    public const string AntimeridianUnsupported = "antimeridian_unsupported";

    public const string PolygonType = "Polygon";
    public const string AreaField = "area";
    public const string CoordinatesField = "area.coordinates";

    /// <summary>3 distinct vertices plus the closing position.</summary>
    public const int MinRingPositions = 4;

    /// <summary>200 vertices plus the closing position.</summary>
    public const int MaxRingPositions = 201;

    public const int MinDistinctVertices = 3;

    /// <summary>Upper bound for the approximate area of a drawn city-scale area.</summary>
    public const double MaxAreaKm2 = 100d;

    /// <summary>Longitude span above which the ring is treated as antimeridian-crossing.</summary>
    public const double MaxLongitudeSpanDegrees = 180d;

    private const double EarthRadiusMeters = 6_371_008.8;
    private const double RadiansPerDegree = Math.PI / 180d;

    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    public static bool TryCreateSearchArea(
        AreaPolygon? area,
        out SearchArea searchArea,
        out AreaValidationError? error)
    {
        searchArea = null!;
        error = null;

        if (area is null)
        {
            error = new AreaValidationError(
                InvalidAreaRing,
                AreaField,
                "area is required and must be a GeoJSON Polygon.");
            return false;
        }

        if (!string.Equals(area.Type, PolygonType, StringComparison.OrdinalIgnoreCase))
        {
            error = new AreaValidationError(
                InvalidAreaType,
                AreaField,
                $"area.type must be \"{PolygonType}\"; other GeoJSON geometry types are not supported.");
            return false;
        }

        var rings = area.Coordinates;
        if (rings is null || rings.Length == 0 || rings[0] is null)
        {
            error = new AreaValidationError(
                InvalidAreaRing,
                CoordinatesField,
                "area.coordinates must contain exactly one closed exterior ring.");
            return false;
        }

        if (rings.Length > 1)
        {
            error = new AreaValidationError(
                UnsupportedAreaHoles,
                CoordinatesField,
                "area.coordinates must contain exactly one ring; polygon holes are not supported.");
            return false;
        }

        var ring = rings[0];
        if (ring.Length < MinRingPositions || ring.Length > MaxRingPositions)
        {
            error = new AreaValidationError(
                InvalidAreaVertexCount,
                CoordinatesField,
                $"area.coordinates must contain between {MinRingPositions} and {MaxRingPositions} positions "
                + "(3 to 200 vertices plus the closing position).");
            return false;
        }

        var coordinates = new Coordinate[ring.Length];
        for (var index = 0; index < ring.Length; index++)
        {
            var position = ring[index];
            if (position is null || position.Length != 2)
            {
                error = new AreaValidationError(
                    InvalidAreaCoordinate,
                    CoordinatesField,
                    $"Position {index} must contain exactly two numbers: [lng, lat].");
                return false;
            }

            var lng = position[0];
            var lat = position[1];
            if (!double.IsFinite(lng) || !double.IsFinite(lat)
                || lng < -180d || lng > 180d || lat < -90d || lat > 90d)
            {
                error = new AreaValidationError(
                    InvalidAreaCoordinate,
                    CoordinatesField,
                    $"Position {index} must be a finite [lng, lat] pair with lng in [-180, 180] and lat in [-90, 90].");
                return false;
            }

            coordinates[index] = new Coordinate(lng, lat);
        }

        if (!coordinates[0].Equals2D(coordinates[^1]))
        {
            error = new AreaValidationError(
                InvalidAreaRing,
                CoordinatesField,
                "area.coordinates ring must be closed: the first and last positions must be identical.");
            return false;
        }

        if (CountDistinctVertices(coordinates) < MinDistinctVertices)
        {
            error = new AreaValidationError(
                InvalidAreaVertexCount,
                CoordinatesField,
                $"area.coordinates ring must contain at least {MinDistinctVertices} distinct vertices.");
            return false;
        }

        var minLng = double.MaxValue;
        var maxLng = double.MinValue;
        foreach (var coordinate in coordinates)
        {
            minLng = Math.Min(minLng, coordinate.X);
            maxLng = Math.Max(maxLng, coordinate.X);
        }

        if (maxLng - minLng > MaxLongitudeSpanDegrees)
        {
            error = new AreaValidationError(
                AntimeridianUnsupported,
                CoordinatesField,
                $"area.coordinates spans more than {Format(MaxLongitudeSpanDegrees, "0")} degrees of longitude; "
                + "rings that cross the antimeridian are not supported.");
            return false;
        }

        Polygon polygon;
        try
        {
            polygon = Factory.CreatePolygon(coordinates);
        }
        catch (Exception ex) when (ex is ArgumentException or TopologyException)
        {
            error = new AreaValidationError(
                InvalidAreaRing,
                CoordinatesField,
                "area.coordinates is not a valid linear ring.");
            return false;
        }

        var approximateAreaKm2 = ComputeApproximateAreaKm2(coordinates);
        if (!(approximateAreaKm2 > 0d))
        {
            error = new AreaValidationError(
                InvalidAreaRing,
                CoordinatesField,
                "area.coordinates encloses no area; the ring is degenerate.");
            return false;
        }

        if (!polygon.IsValid)
        {
            error = new AreaValidationError(
                SelfIntersectingArea,
                CoordinatesField,
                "area.coordinates is self-intersecting or otherwise not a valid polygon.");
            return false;
        }

        if (approximateAreaKm2 > MaxAreaKm2)
        {
            error = new AreaValidationError(
                AreaTooLarge,
                CoordinatesField,
                $"area.coordinates covers about {Format(approximateAreaKm2, "0.#")} km²; "
                + $"the maximum supported area is {Format(MaxAreaKm2, "0")} km².");
            return false;
        }

        searchArea = new SearchArea(polygon, PreparedGeometryFactory.Prepare(polygon), approximateAreaKm2);
        return true;
    }

    // Problem details are part of the API contract, so numbers never follow the
    // server culture.
    private static string Format(double value, string format)
        => value.ToString(format, CultureInfo.InvariantCulture);

    private static int CountDistinctVertices(Coordinate[] ring)
    {
        var distinct = new HashSet<(double Lng, double Lat)>();
        for (var index = 0; index < ring.Length - 1; index++)
        {
            distinct.Add((ring[index].X, ring[index].Y));
        }

        return distinct.Count;
    }

    /// <summary>
    /// Shoelace area of the ring projected to metres with a local equirectangular
    /// approximation (x scaled by cos(mean latitude)). Valid for city-scale areas;
    /// it is not a geodesic area and not accurate near the poles.
    /// </summary>
    private static double ComputeApproximateAreaKm2(Coordinate[] ring)
    {
        var vertexCount = ring.Length - 1;

        var meanLat = 0d;
        for (var index = 0; index < vertexCount; index++)
        {
            meanLat += ring[index].Y;
        }

        meanLat /= vertexCount;
        var lngScale = RadiansPerDegree * EarthRadiusMeters * Math.Cos(meanLat * RadiansPerDegree);

        var twiceArea = 0d;
        for (var index = 0; index < vertexCount; index++)
        {
            var current = ring[index];
            var next = ring[index + 1];
            var currentX = current.X * lngScale;
            var currentY = current.Y * RadiansPerDegree * EarthRadiusMeters;
            var nextX = next.X * lngScale;
            var nextY = next.Y * RadiansPerDegree * EarthRadiusMeters;
            twiceArea += currentX * nextY - nextX * currentY;
        }

        return Math.Abs(twiceArea) / 2d / 1_000_000d;
    }
}
