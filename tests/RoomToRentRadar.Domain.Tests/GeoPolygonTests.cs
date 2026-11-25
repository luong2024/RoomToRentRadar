namespace RoomToRentRadar.Domain.Tests;

using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

[TestFixture]
public class GeoPolygonTests
{
    // A valid ~10,000 m² polygon near ICTU Campus (Thai Nguyen)
    private static readonly GeoLocation PointA = new(21.5850, 105.8070);
    private static readonly GeoLocation PointB = new(21.5860, 105.8070);
    private static readonly GeoLocation PointC = new(21.5860, 105.8080);
    private static readonly GeoLocation PointD = new(21.5850, 105.8080);

    [Test]
    public void Constructor_WithValidCoordinates_CreatesClosedCCWPolygon()
    {
        // Counter-Clockwise input: A -> D -> C -> B
        var coords = new[] { PointA, PointD, PointC, PointB };

        var polygon = new GeoPolygon(coords);

        Assert.Multiple(() =>
        {
            Assert.That(polygon.DistinctVertices, Has.Count.EqualTo(4));
            Assert.That(polygon.Vertices, Has.Count.EqualTo(5)); // Closed ring
            Assert.That(polygon.Vertices[0].Latitude, Is.EqualTo(polygon.Vertices[^1].Latitude));
            Assert.That(polygon.Vertices[0].Longitude, Is.EqualTo(polygon.Vertices[^1].Longitude));
            Assert.That(polygon.AreaSquareMeters, Is.GreaterThanOrEqualTo(400.0));
        });
    }

    [Test]
    public void Constructor_WithClockwiseCoordinates_NormalizesToCounterClockwise()
    {
        // Clockwise input: A -> B -> C -> D
        var cwCoords = new[] { PointA, PointB, PointC, PointD };
        // Counter-Clockwise input: A -> D -> C -> B
        var ccwCoords = new[] { PointA, PointD, PointC, PointB };

        var polygonFromCw = new GeoPolygon(cwCoords);
        var polygonFromCcw = new GeoPolygon(ccwCoords);

        Assert.That(polygonFromCw.AreaSquareMeters, Is.EqualTo(polygonFromCcw.AreaSquareMeters).Within(1.0));
        Assert.That(polygonFromCw.Vertices, Has.Count.EqualTo(5));
    }

    [Test]
    public void Constructor_WithExplicitClosingPoint_DoesNotDuplicateClosure()
    {
        // A -> D -> C -> B -> A (already closed)
        var closedCoords = new[] { PointA, PointD, PointC, PointB, PointA };

        var polygon = new GeoPolygon(closedCoords);

        Assert.Multiple(() =>
        {
            Assert.That(polygon.DistinctVertices, Has.Count.EqualTo(4));
            Assert.That(polygon.Vertices, Has.Count.EqualTo(5));
        });
    }

    [Test]
    public void Constructor_WithLessThanThreeVertices_ThrowsInvalidPolygonException()
    {
        var coords = new[] { PointA, PointB };

        var ex = Assert.Throws<InvalidPolygonException>(() => new GeoPolygon(coords));
        Assert.That(ex.Message, Does.Contain("at least 3 distinct coordinates"));
    }

    [Test]
    public void Constructor_WithTinySubThresholdArea_ThrowsInvalidPolygonException()
    {
        // Small triangle with area ~ 60 m² (< 400 m² threshold)
        // 0.0001 degrees latitude is ~ 11 meters
        var tinyA = new GeoLocation(21.5850, 105.8070);
        var tinyB = new GeoLocation(21.5851, 105.8070);
        var tinyC = new GeoLocation(21.5850, 105.8071);

        var ex = Assert.Throws<InvalidPolygonException>(() => new GeoPolygon(new[] { tinyA, tinyB, tinyC }));
        Assert.That(ex.Message, Does.Contain("area is too small"));
    }

    [TestCase(-86.0, 105.0)]
    [TestCase(86.0, 105.0)]
    public void Constructor_WithLatitudeOutsideWebMercatorBounds_ThrowsInvalidPolygonException(double lat, double lon)
    {
        // GeoLocation constructor checks [-90, 90], GeoPolygon checks [-85.0511, 85.0511]
        var outOfBoundsPoint = new GeoLocation(lat, lon);
        var coords = new[] { outOfBoundsPoint, PointB, PointC, PointD };

        var ex = Assert.Throws<InvalidPolygonException>(() => new GeoPolygon(coords));
        Assert.That(ex.Message, Does.Contain("Web Mercator"));
    }
}
