using RoomToRentRadar.Api.Features.Listings;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class GeoDistanceTests
{
    private const double HoChiMinhLat = 10.7769;
    private const double HoChiMinhLng = 106.7009;

    [Test]
    public void HaversineKm_ZeroForIdenticalPoints()
        => Assert.That(
            GeoDistance.HaversineKm(HoChiMinhLat, HoChiMinhLng, HoChiMinhLat, HoChiMinhLng),
            Is.EqualTo(0d).Within(1e-9));

    [Test]
    public void HaversineKm_OneDegreeOfLatitude_IsAboutOneHundredAndElevenKilometres()
    {
        var distance = GeoDistance.HaversineKm(HoChiMinhLat, HoChiMinhLng, HoChiMinhLat + 1d, HoChiMinhLng);

        Assert.That(distance, Is.EqualTo(GeoDistance.EarthRadiusKm * Math.PI / 180d).Within(0.001));
        Assert.That(distance, Is.EqualTo(111.19).Within(0.05));
    }

    [Test]
    public void HaversineKm_KnownCityPair_MatchesReferenceValue()
    {
        // Ho Chi Minh City to Ha Noi on a sphere with the mean earth radius: 1143 km.
        var distance = GeoDistance.HaversineKm(10.7769, 106.7009, 21.0285, 105.8542);

        Assert.That(distance, Is.EqualTo(1143.4).Within(2.0));
    }

    [Test]
    public void HaversineKm_IsSymmetric()
    {
        var forward = GeoDistance.HaversineKm(HoChiMinhLat, HoChiMinhLng, 21.0285, 105.8542);
        var backward = GeoDistance.HaversineKm(21.0285, 105.8542, HoChiMinhLat, HoChiMinhLng);

        Assert.That(forward, Is.EqualTo(backward).Within(1e-9));
    }

    [Test]
    public void BoundingBox_ContainsPointsInsideTheRadius()
    {
        var box = GeoDistance.BoundingBox(HoChiMinhLat, HoChiMinhLng, radiusKm: 5d);

        // Roughly 3 km north, south, east and west of the centre.
        var north = HoChiMinhLat + 3d / GeoDistance.KmPerDegreeLatitude;
        var east = HoChiMinhLng + 3d / (GeoDistance.KmPerDegreeLatitude * Math.Cos(ToRadians(HoChiMinhLat)));

        Assert.Multiple(() =>
        {
            Assert.That(box.MinLat, Is.LessThan(HoChiMinhLat));
            Assert.That(box.MaxLat, Is.GreaterThan(north));
            Assert.That(box.MinLng, Is.LessThan(HoChiMinhLng));
            Assert.That(box.MaxLng, Is.GreaterThan(east));
        });
    }

    [Test]
    public void BoundingBox_DoesNotContainPointsBeyondTheRadius()
    {
        var box = GeoDistance.BoundingBox(HoChiMinhLat, HoChiMinhLng, radiusKm: 2d);

        var farNorth = HoChiMinhLat + 5d / GeoDistance.KmPerDegreeLatitude;
        var farWest = HoChiMinhLng - 5d / (GeoDistance.KmPerDegreeLatitude * Math.Cos(ToRadians(HoChiMinhLat)));

        Assert.Multiple(() =>
        {
            Assert.That(farNorth, Is.GreaterThan(box.MaxLat));
            Assert.That(farWest, Is.LessThan(box.MinLng));
        });
    }

    [Test]
    public void BoundingBox_BoundsMatchTheRequestedDelta()
    {
        var box = GeoDistance.BoundingBox(HoChiMinhLat, HoChiMinhLng, radiusKm: 10d);

        var expectedLatDelta = 10d / GeoDistance.KmPerDegreeLatitude;
        var expectedLngDelta = 10d / (GeoDistance.KmPerDegreeLatitude * Math.Cos(ToRadians(HoChiMinhLat)));

        Assert.Multiple(() =>
        {
            Assert.That(box.MaxLat - HoChiMinhLat, Is.EqualTo(expectedLatDelta).Within(1e-12));
            Assert.That(HoChiMinhLat - box.MinLat, Is.EqualTo(expectedLatDelta).Within(1e-12));
            Assert.That(box.MaxLng - HoChiMinhLng, Is.EqualTo(expectedLngDelta).Within(1e-12));
            Assert.That(HoChiMinhLng - box.MinLng, Is.EqualTo(expectedLngDelta).Within(1e-12));
        });
    }

    [Test]
    public void BoundingBox_LongitudeDeltaIsClampedToTheValidRange()
    {
        // Near the antimeridian a 50 km box would wrap beyond 180 degrees.
        var box = GeoDistance.BoundingBox(10d, 179.9, radiusKm: 50d);

        Assert.Multiple(() =>
        {
            Assert.That(box.MaxLng, Is.EqualTo(180d));
            Assert.That(box.MinLng, Is.LessThan(179.9));
            Assert.That(box.MinLng, Is.GreaterThanOrEqualTo(-180d));
        });
    }

    [Test]
    public void BoundingBox_LatitudeDeltaIsClampedToTheValidRange()
    {
        var box = GeoDistance.BoundingBox(89.9, 10d, radiusKm: 50d);

        Assert.Multiple(() =>
        {
            Assert.That(box.MaxLat, Is.EqualTo(90d));
            Assert.That(box.MinLat, Is.GreaterThanOrEqualTo(-90d));
        });
    }

    [Test]
    public void BoundingBox_NearThePole_SpansEveryLongitude()
    {
        var box = GeoDistance.BoundingBox(90d, 0d, radiusKm: 1d);

        Assert.Multiple(() =>
        {
            Assert.That(box.MinLng, Is.EqualTo(-180d));
            Assert.That(box.MaxLng, Is.EqualTo(180d));
            Assert.That(box.MinLat, Is.GreaterThan(89d));
        });
    }

    [Test]
    public void HaversineKm_BoundaryComparisonIsInclusive()
    {
        // The radius filter keeps rows whose distance is less than or equal to the
        // requested radius, so an exact boundary match must survive the comparison.
        var target = HoChiMinhLat + 2d / GeoDistance.KmPerDegreeLatitude;
        var distance = GeoDistance.HaversineKm(HoChiMinhLat, HoChiMinhLng, target, HoChiMinhLng);

        Assert.Multiple(() =>
        {
            Assert.That(IsWithinRadius(distance, distance), Is.True, "the boundary is included");
            Assert.That(
                IsWithinRadius(distance, Math.BitDecrement(distance)),
                Is.False,
                "a marginally smaller radius excludes the boundary");
        });
    }

    /// <summary>Mirrors the inclusive predicate used by the radius filter.</summary>
    private static bool IsWithinRadius(double distanceKm, double radiusKm) => distanceKm <= radiusKm;

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}
