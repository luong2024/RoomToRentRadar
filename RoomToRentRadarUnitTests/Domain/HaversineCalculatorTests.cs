using RoomToRentRadar.Domain.Services;

namespace RoomToRentRadarUnitTests.Domain;

[TestFixture]
public class HaversineCalculatorTests
{
    [Test]
    public void DistanceKm_WithKnownCoordinates_ReturnsExpectedDistance()
    {
        // ICTU coordinates: (21.5851, 105.8066)
        // Room near ICTU: (21.5875, 105.8080) ~ 0.3 km
        var distance = HaversineCalculator.DistanceKm(21.5851, 105.8066, 21.5875, 105.8080);

        Assert.That(distance, Is.GreaterThan(0.2));
        Assert.That(distance, Is.LessThan(0.5));
    }

    [TestCase(0.0, 0.0, 21.5851, 105.8066)]
    [TestCase(21.5851, 105.8066, 0.0, 0.0)]
    [TestCase(0.0, 0.0, 0.0, 0.0)]
    public void DistanceKm_WhenEitherCoordinateIsZero_ReturnsZeroGuard(
        double lat1, double lon1, double lat2, double lon2)
    {
        var distance = HaversineCalculator.DistanceKm(lat1, lon1, lat2, lon2);
        Assert.That(distance, Is.EqualTo(0.0));
    }

    [Test]
    public void DistanceKm_IsSymmetric_DistanceAtoBEqualsDistanceBtoA()
    {
        var latA = 21.5851;
        var lonA = 105.8066;
        var latB = 21.5887;
        var lonB = 105.80979;

        var distAB = HaversineCalculator.DistanceKm(latA, lonA, latB, lonB);
        var distBA = HaversineCalculator.DistanceKm(latB, lonB, latA, lonA);

        Assert.That(distAB, Is.EqualTo(distBA));
    }

    [Test]
    public void DistanceKm_SameCoordinates_ReturnsZero()
    {
        var distance = HaversineCalculator.DistanceKm(21.5851, 105.8066, 21.5851, 105.8066);
        Assert.That(distance, Is.EqualTo(0.0));
    }
}
