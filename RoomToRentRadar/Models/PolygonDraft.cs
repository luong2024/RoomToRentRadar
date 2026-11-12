using NetTopologySuite.Geometries;

namespace RoomToRentRadar.Models;

/// <summary>
/// Vertices collected while drawing a search polygon. The draft can be edited
/// until <see cref="Close"/> succeeds; afterwards it is immutable until cleared.
/// </summary>
public sealed class PolygonDraft
{
    public const int MinimumVertices = 3;

    private readonly List<GeoPoint> _vertices = [];

    public IReadOnlyList<GeoPoint> Vertices => _vertices;

    public bool IsClosed { get; private set; }

    public bool CanClose => !IsClosed && _vertices.Count >= MinimumVertices;

    internal bool HasValidGeometry
    {
        get
        {
            if (_vertices.Count < MinimumVertices || _vertices.Distinct().Count() != _vertices.Count)
                return false;
            var coordinates = _vertices.Select(vertex => new Coordinate(vertex.Lng, vertex.Lat))
                .Append(new Coordinate(_vertices[0].Lng, _vertices[0].Lat)).ToArray();
            var polygon = new GeometryFactory().CreatePolygon(coordinates);
            return polygon.IsValid && polygon.Area > 0;
        }
    }

    /// <summary>Vertices plus the repeated first vertex when closed.</summary>
    public IReadOnlyList<GeoPoint> Ring => IsClosed && _vertices.Count > 0
        ? [.. _vertices, _vertices[0]]
        : _vertices;

    public bool Add(GeoPoint vertex)
    {
        if (IsClosed || !vertex.IsValid)
            return false;

        if (_vertices.Count > 0 && _vertices[^1] == vertex)
            return false;

        _vertices.Add(vertex);
        return true;
    }

    public bool RemoveLast()
    {
        if (IsClosed || _vertices.Count == 0)
            return false;

        _vertices.RemoveAt(_vertices.Count - 1);
        return true;
    }

    public bool Close()
    {
        if (!CanClose)
            return false;

        IsClosed = true;
        return true;
    }

    public void Clear()
    {
        _vertices.Clear();
        IsClosed = false;
    }

    public bool Load(IEnumerable<GeoPoint> vertices)
    {
        Clear();
        foreach (var vertex in vertices)
            Add(vertex);

        return Close();
    }

    /// <summary>
    /// Area-weighted centroid (shoelace formula). Degenerate rings fall back to the
    /// average of their vertices.
    /// </summary>
    public GeoPoint Centroid
    {
        get
        {
            if (_vertices.Count == 0)
                return new GeoPoint(0, 0);

            if (_vertices.Count < MinimumVertices)
                return new GeoPoint(_vertices.Average(v => v.Lat), _vertices.Average(v => v.Lng));

            double twiceArea = 0;
            double weightedLng = 0;
            double weightedLat = 0;
            var ring = Ring;
            for (var i = 0; i < ring.Count - 1; i++)
            {
                var (lat1, lng1) = ring[i];
                var (lat2, lng2) = ring[i + 1];
                var cross = lng1 * lat2 - lng2 * lat1;
                twiceArea += cross;
                weightedLng += (lng1 + lng2) * cross;
                weightedLat += (lat1 + lat2) * cross;
            }

            if (Math.Abs(twiceArea) < 1e-12)
                return new GeoPoint(_vertices.Average(v => v.Lat), _vertices.Average(v => v.Lng));

            var factor = 1d / (3d * twiceArea);
            return new GeoPoint(weightedLat * factor, weightedLng * factor);
        }
    }

    /// <summary>GeoJSON coordinates: one closed ring of <c>[lng, lat]</c> positions.</summary>
    public double[][][] ToGeoJsonCoordinates()
    {
        if (!IsClosed)
            throw new InvalidOperationException("Close the polygon before searching.");

        var ring = Ring.Select(v => new[] { v.Lng, v.Lat }).ToArray();
        return [ring];
    }
}
