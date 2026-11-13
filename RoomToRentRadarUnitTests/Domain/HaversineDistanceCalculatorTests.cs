using RoomToRentRadar.Domain.Services;

namespace RoomToRentRadarUnitTests.Domain;

/// <summary>H3 / BR-CMP-03: distance must be finite-safe and rounded to 0.1 km (BR-SRCH-03).</summary>
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
    public void CalculateDistanceKm_IctuToZ115_ReturnsZeroPointThreeKm()
    {
        // ICTU (21.5851, 105.8066) -> phòng Z115 (21.5875, 105.8080) ≈ 0.3036 km -> 0.3 sau làm tròn 0.1 km (BR-SRCH-03).
        var distance = HaversineDistanceCalculator.CalculateDistanceKm(IctuLat, IctuLng, 21.5875, 105.8080);

        Assert.That(distance, Is.EqualTo(0.3));
    }

    [Test]
    public void CalculateDistanceKm_IsSymmetric()
    {
        var forward = HaversineDistanceCalculator.CalculateDistanceKm(IctuLat, IctuLng, 21.5710, 105.8190);
        var backward = HaversineDistanceCalculator.CalculateDistanceKm(21.5710, 105.8190, IctuLat, IctuLng);

        Assert.That(forward, Is.EqualTo(backward));
    }

    [Test]
    public void CalculateDistanceKm_AntipodalPoints_ReturnsHalfCircumferenceWithoutNaN()
    {
        var distance = HaversineDistanceCalculator.CalculateDistanceKm(IctuLat, IctuLng, -IctuLat, IctuLng - 180);

        Assert.Multiple(() =>
        {
            Assert.That(double.IsNaN(distance), Is.False, "Điểm đối cực không được trả NaN (clamp [0,1] trước Sqrt).");
            Assert.That(distance, Is.EqualTo(20015.09).Within(1.0), "Nửa chu vi Trái Đất ≈ 20,015 km");
        });
    }

    [Test]
    public void CalculateDistanceKm_PolesAndDateLine_AreFinite()
    {
        var northToSouth = HaversineDistanceCalculator.CalculateDistanceKm(90, 0, -90, 0);
        var acrossDateLine = HaversineDistanceCalculator.CalculateDistanceKm(21.5851, 179.9999, 21.5851, -179.9999);

        Assert.Multiple(() =>
        {
            Assert.That(double.IsFinite(northToSouth), Is.True);
            Assert.That(northToSouth, Is.EqualTo(20015.09).Within(1.0));
            Assert.That(double.IsFinite(acrossDateLine), Is.True);
            Assert.That(acrossDateLine, Is.LessThan(0.1));
        });
    }

    [TestCase(double.NaN, 105.8066, 21.5875, 105.8080, TestName = "CalculateDistanceKm_Lat1NaN_ReturnsNaN")]
    [TestCase(double.PositiveInfinity, 105.8066, 21.5875, 105.8080, TestName = "CalculateDistanceKm_Lat1Infinity_ReturnsNaN")]
    [TestCase(21.5851, double.NegativeInfinity, 21.5875, 105.8080, TestName = "CalculateDistanceKm_Lng1Infinity_ReturnsNaN")]
    [TestCase(21.5851, 105.8066, double.NaN, 105.8080, TestName = "CalculateDistanceKm_Lat2NaN_ReturnsNaN")]
    [TestCase(21.5851, 105.8066, 21.5875, double.PositiveInfinity, TestName = "CalculateDistanceKm_Lng2Infinity_ReturnsNaN")]
    [TestCase(21.5851, 105.8066, 21.5875, double.NegativeInfinity, TestName = "CalculateDistanceKm_Lng2NegativeInfinity_ReturnsNaN")]
    public void CalculateDistanceKm_NonFiniteInput_ReturnsNaN(double lat1, double lon1, double lat2, double lon2)
    {
        var distance = HaversineDistanceCalculator.CalculateDistanceKm(lat1, lon1, lat2, lon2);

        Assert.That(double.IsNaN(distance), Is.True);
    }

    [Test]
    public void CalculateDistanceKm_IsRoundedToOneDecimalPlace()
    {
        var samples = new[]
        {
            (21.5875, 105.8080),
            (21.5710, 105.8190),
            (21.5600, 105.8250),
            (21.59861, 105.82163),
            (21.54954, 105.84350)
        };

        foreach (var (lat, lng) in samples)
        {
            var distance = HaversineDistanceCalculator.CalculateDistanceKm(IctuLat, IctuLng, lat, lng);
            Assert.That(Math.Round(distance, 1), Is.EqualTo(distance), $"({lat}, {lng})");
        }
    }
}
