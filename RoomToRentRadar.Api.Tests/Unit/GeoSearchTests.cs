using System.Globalization;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class GeoSearchTests
{
    private const double MinLng = 106.68;
    private const double MinLat = 10.75;
    private const double MaxLng = 106.72;
    private const double MaxLat = 10.79;

    [Test]
    public void ValidRectangle_IsAccepted()
    {
        Assert.That(GeoSearch.TryCreateSearchArea(Rectangle(), out var area, out var error), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.Null);
            Assert.That(area.MinLng, Is.EqualTo(MinLng));
            Assert.That(area.MaxLng, Is.EqualTo(MaxLng));
            Assert.That(area.MinLat, Is.EqualTo(MinLat));
            Assert.That(area.MaxLat, Is.EqualTo(MaxLat));
            Assert.That(area.Polygon.SRID, Is.EqualTo(4326));
        });
    }

    [Test]
    public void Centroid_IsTheDistanceReferencePoint()
    {
        Assert.That(GeoSearch.TryCreateSearchArea(Rectangle(), out var area, out _), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(area.CentroidLng, Is.EqualTo(106.70).Within(1e-9));
            Assert.That(area.CentroidLat, Is.EqualTo(10.77).Within(1e-9));
        });
    }

    [Test]
    public void LowercasePolygonType_IsAccepted()
    {
        Assert.That(GeoSearch.TryCreateSearchArea(Rectangle("polygon"), out _, out _), Is.True);
    }

    [Test]
    public void ApproximateArea_UsesEquirectangularProjection()
    {
        // 0.04 x 0.04 degrees at latitude 10.77: 4.369 km east/west by 4.448 km north/south.
        Assert.That(GeoSearch.TryCreateSearchArea(Rectangle(), out var large, out _), Is.True);
        Assert.That(large.ApproximateAreaKm2, Is.EqualTo(19.44).Within(0.1));

        var small = PolygonOf(
            (MinLng, MinLat),
            (MinLng + 0.01, MinLat),
            (MinLng + 0.01, MinLat + 0.01),
            (MinLng, MinLat + 0.01),
            (MinLng, MinLat));

        Assert.That(GeoSearch.TryCreateSearchArea(small, out var smallArea, out _), Is.True);
        Assert.That(smallArea.ApproximateAreaKm2, Is.EqualTo(1.215).Within(0.01));
    }

    [TestCase(106.70, 10.77, true, TestName = "Inside_IsIncluded")]
    [TestCase(106.69, 10.76, true, TestName = "InsideNearCorner_IsIncluded")]
    [TestCase(106.680001, 10.77, true, TestName = "JustInsideWestEdge_IsIncluded")]
    [TestCase(106.68, 10.77, true, TestName = "WestEdge_IsIncluded")]
    [TestCase(106.72, 10.77, true, TestName = "EastEdge_IsIncluded")]
    [TestCase(106.70, 10.75, true, TestName = "SouthEdge_IsIncluded")]
    [TestCase(106.70, 10.79, true, TestName = "NorthEdge_IsIncluded")]
    [TestCase(106.68, 10.75, true, TestName = "SouthWestVertex_IsIncluded")]
    [TestCase(106.72, 10.79, true, TestName = "NorthEastVertex_IsIncluded")]
    [TestCase(106.6799, 10.77, false, TestName = "JustWestOfEdge_IsExcluded")]
    [TestCase(106.7201, 10.77, false, TestName = "JustEastOfEdge_IsExcluded")]
    [TestCase(106.70, 10.7499, false, TestName = "JustSouthOfEdge_IsExcluded")]
    [TestCase(106.70, 10.7901, false, TestName = "JustNorthOfEdge_IsExcluded")]
    [TestCase(106.60, 10.77, false, TestName = "FarOutside_IsExcluded")]
    [TestCase(106.68, 10.80, false, TestName = "OutsideDiagonal_IsExcluded")]
    public void PointClassification_IncludesBoundary(double lng, double lat, bool expected)
    {
        Assert.That(GeoSearch.TryCreateSearchArea(Rectangle(), out var area, out _), Is.True);
        Assert.That(area.Intersects(lng, lat), Is.EqualTo(expected));
    }

    [Test]
    public void WrongType_IsRejected()
    {
        AssertRejected(Rectangle("LineString"), GeoSearch.InvalidAreaType, GeoSearch.AreaField);
    }

    [Test]
    public void UnclosedRing_IsRejected()
    {
        var polygon = PolygonOf(
            (MinLng, MinLat),
            (MaxLng, MinLat),
            (MaxLng, MaxLat),
            (MinLng, MaxLat),
            (MinLng, MinLat + 0.001));

        AssertRejected(polygon, GeoSearch.InvalidAreaRing, GeoSearch.CoordinatesField);
    }

    [Test]
    public void TooFewPositions_IsRejected()
    {
        var polygon = PolygonOf((MinLng, MinLat), (MaxLng, MinLat), (MinLng, MinLat));

        AssertRejected(polygon, GeoSearch.InvalidAreaVertexCount, GeoSearch.CoordinatesField);
    }

    [Test]
    public void TooManyPositions_IsRejected()
    {
        // 201 vertices plus the closing position exceeds the 201 position limit.
        var polygon = Polygon(CircleRing(MinLng + 0.02, MinLat + 0.02, 0.01, vertexCount: 201));

        AssertRejected(polygon, GeoSearch.InvalidAreaVertexCount, GeoSearch.CoordinatesField);
    }

    [Test]
    public void MaximumPositions_AreAccepted()
    {
        // 200 vertices plus the closing position is exactly the limit.
        var polygon = Polygon(CircleRing(MinLng + 0.02, MinLat + 0.02, 0.01, vertexCount: 200));

        Assert.That(GeoSearch.TryCreateSearchArea(polygon, out var area, out var error), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(error, Is.Null);
            Assert.That(area.Intersects(MinLng + 0.02, MinLat + 0.02), Is.True);
            Assert.That(area.Intersects(MinLng, MinLat), Is.False);
        });
    }

    [TestCase(double.NaN, 10.77)]
    [TestCase(double.PositiveInfinity, 10.77)]
    [TestCase(106.70, double.NegativeInfinity)]
    public void NonFiniteCoordinate_IsRejected(double lng, double lat)
    {
        var polygon = PolygonOf(
            (MinLng, MinLat),
            (MaxLng, MinLat),
            (lng, lat),
            (MinLng, MaxLat),
            (MinLng, MinLat));

        AssertRejected(polygon, GeoSearch.InvalidAreaCoordinate, GeoSearch.CoordinatesField);
    }

    [TestCase(180.5, 10.77)]
    [TestCase(-180.5, 10.77)]
    [TestCase(106.70, 90.5)]
    [TestCase(106.70, -90.5)]
    public void OutOfRangeCoordinate_IsRejected(double lng, double lat)
    {
        var polygon = PolygonOf(
            (MinLng, MinLat),
            (MaxLng, MinLat),
            (lng, lat),
            (MinLng, MaxLat),
            (MinLng, MinLat));

        AssertRejected(polygon, GeoSearch.InvalidAreaCoordinate, GeoSearch.CoordinatesField);
    }

    [Test]
    public void PositionWithThreeNumbers_IsRejected()
    {
        var polygon = new AreaPolygon
        {
            Type = "Polygon",
            Coordinates =
            [
                [
                    new[] { MinLng, MinLat },
                    new[] { MaxLng, MinLat, 0d },
                    new[] { MaxLng, MaxLat },
                    new[] { MinLng, MaxLat },
                    new[] { MinLng, MinLat }
                ]
            ]
        };

        AssertRejected(polygon, GeoSearch.InvalidAreaCoordinate, GeoSearch.CoordinatesField);
    }

    [Test]
    public void SelfIntersectingPolygon_IsRejected()
    {
        // A spike that crosses the south edge; the net signed area is not zero, so
        // the failure is reported as a self-intersection rather than a degenerate ring.
        var polygon = PolygonOf(
            (106.68, 10.75),
            (106.72, 10.75),
            (106.72, 10.79),
            (106.70, 10.74),
            (106.68, 10.79),
            (106.68, 10.75));

        AssertRejected(polygon, GeoSearch.SelfIntersectingArea, GeoSearch.CoordinatesField);
    }

    [Test]
    public void DegenerateRing_IsRejected()
    {
        var polygon = PolygonOf((106.68, 10.75), (106.70, 10.75), (106.72, 10.75), (106.68, 10.75));

        AssertRejected(polygon, GeoSearch.InvalidAreaRing, GeoSearch.CoordinatesField);
    }

    [Test]
    public void Holes_AreRejected()
    {
        var polygon = new AreaPolygon
        {
            Type = "Polygon",
            Coordinates =
            [
                RingOf((MinLng, MinLat), (MaxLng, MinLat), (MaxLng, MaxLat), (MinLng, MaxLat), (MinLng, MinLat)),
                RingOf((106.69, 10.76), (106.71, 10.76), (106.71, 10.78), (106.69, 10.78), (106.69, 10.76))
            ]
        };

        AssertRejected(polygon, GeoSearch.UnsupportedAreaHoles, GeoSearch.CoordinatesField);
    }

    [Test]
    public void EmptyRings_AreRejected()
    {
        var polygon = new AreaPolygon { Type = "Polygon", Coordinates = [] };

        AssertRejected(polygon, GeoSearch.InvalidAreaRing, GeoSearch.CoordinatesField);
    }

    [Test]
    public void MissingArea_IsRejected()
        => Assert.That(
            GeoSearch.TryCreateSearchArea(null, out _, out var error),
            Is.False,
            error?.Detail);

    [Test]
    public void AreaTooLarge_IsRejected()
    {
        // Roughly 55 km by 56 km, about 3000 km².
        var polygon = PolygonOf(
            (MinLng, MinLat),
            (MinLng + 0.5, MinLat),
            (MinLng + 0.5, MinLat + 0.5),
            (MinLng, MinLat + 0.5),
            (MinLng, MinLat));

        AssertRejected(polygon, GeoSearch.AreaTooLarge, GeoSearch.CoordinatesField);
    }

    [Test]
    public void ValidationMessages_UseInvariantNumberFormatting()
    {
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var tooLarge = PolygonOf(
                (MinLng, MinLat),
                (MinLng + 0.5, MinLat),
                (MinLng + 0.5, MinLat + 0.5),
                (MinLng, MinLat + 0.5),
                (MinLng, MinLat));

            Assert.That(GeoSearch.TryCreateSearchArea(tooLarge, out _, out var error), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(error!.Detail, Does.Contain("100 km"));
                Assert.That(error.Detail, Does.Not.Contain(","));
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Test]
    public void AntimeridianSpan_IsRejected()
    {
        var polygon = PolygonOf((-179d, 10d), (179d, 10d), (179d, 11d), (-179d, 11d), (-179d, 10d));

        AssertRejected(polygon, GeoSearch.AntimeridianUnsupported, GeoSearch.CoordinatesField);
    }

    [Test]
    public void DuplicateVertices_AreRejected()
    {
        var polygon = PolygonOf((MinLng, MinLat), (MaxLng, MinLat), (MaxLng, MinLat), (MinLng, MinLat));

        AssertRejected(polygon, GeoSearch.InvalidAreaVertexCount, GeoSearch.CoordinatesField);
    }

    private static void AssertRejected(AreaPolygon polygon, string expectedCode, string expectedField)
    {
        Assert.That(GeoSearch.TryCreateSearchArea(polygon, out _, out var error), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.Not.Null);
            Assert.That(error!.Code, Is.EqualTo(expectedCode));
            Assert.That(error.Field, Is.EqualTo(expectedField));
            Assert.That(error.Detail, Is.Not.Empty);
        });
    }

    private static AreaPolygon Rectangle(string type = "Polygon")
        => PolygonOf(
            type,
            (MinLng, MinLat),
            (MaxLng, MinLat),
            (MaxLng, MaxLat),
            (MinLng, MaxLat),
            (MinLng, MinLat));

    private static AreaPolygon Polygon(double[][] ring) => Polygon("Polygon", ring);

    private static AreaPolygon Polygon(string type, double[][] ring)
        => new() { Type = type, Coordinates = [ring] };

    private static AreaPolygon PolygonOf(params (double Lng, double Lat)[] positions)
        => PolygonOf("Polygon", positions);

    private static AreaPolygon PolygonOf(string type, params (double Lng, double Lat)[] positions)
        => Polygon(type, RingOf(positions));

    private static double[][] RingOf(params (double Lng, double Lat)[] positions)
        => positions.Select(position => new[] { position.Lng, position.Lat }).ToArray();

    private static double[][] CircleRing(double centerLng, double centerLat, double radiusDegrees, int vertexCount)
    {
        var ring = new double[vertexCount + 1][];
        for (var index = 0; index < vertexCount; index++)
        {
            var angle = 2 * Math.PI * index / vertexCount;
            ring[index] =
            [
                centerLng + radiusDegrees * Math.Cos(angle),
                centerLat + radiusDegrees * Math.Sin(angle)
            ];
        }

        ring[vertexCount] = [ring[0][0], ring[0][1]];
        return ring;
    }
}
