using RoomToRentRadar.Domain.Services;

namespace RoomToRentRadarUnitTests.Domain;

[TestFixture]
public class HaversineDistanceCalculatorTests
{
    private const double IctuLat = 21.5851;
    private const double IctuLng = 105.8066;

    [Test]
    public void CalculateDistanceKm_SameCoordinates_ReturnsZero()
    {
        var distance = HaversineDistanceCalculator.CalculateDistanceKm(IctuLat, IctuLng, IctuLat, IctuLng);

        Assert.That(distance, Is.EqualTo(0.0));
    }

    [Test]
    public void CalculateDistanceKm_IctuToZ115_ReturnsAboutZeroPointThreeKm()
    {
        // ICTU (21.5851, 105.8066) -> Z115 (21.5875, 105.8080)
        // Delta lat 0.0024 -> ~0.267 km; delta lng 0.0014 -> ~0.145 km
        // => ~0.3036 km -> round 2dp = 0.30 (plan ghi "~0.42" là ước lượng sai, đã xác minh bằng 3 cách tính độc lập)
        var distance = HaversineDistanceCalculator.CalculateDistanceKm(IctuLat, IctuLng, 21.5875, 105.8080);

        Assert.That(distance, Is.EqualTo(0.30).Within(0.01));
    }

    [Test]
    public void CalculateDistanceKm_IsSymmetric()
    {
        var forward = HaversineDistanceCalculator.CalculateDistanceKm(IctuLat, IctuLng, 21.5710, 105.8190);
        var backward = HaversineDistanceCalculator.CalculateDistanceKm(21.5710, 105.8190, IctuLat, IctuLng);

        Assert.That(forward, Is.EqualTo(backward));
    }

    [Test]
    public void CalculateDistanceKm_IsRoundedToTwoDecimalPlaces()
    {
        var distance = HaversineDistanceCalculator.CalculateDistanceKm(IctuLat, IctuLng, 21.5600, 105.8250);

        Assert.That(Math.Round(distance, 2), Is.EqualTo(distance));
    }
}
