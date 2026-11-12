namespace RoomToRentRadar.Domain.Tests;

using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

[TestFixture]
public class GeoLocationTests
{
    [Test]
    public void Constructor_WithValidCoordinates_ShouldSetProperties()
    {
        var location = new GeoLocation(21.5852, 105.8074);

        Assert.Multiple(() =>
        {
            Assert.That(location.Latitude, Is.EqualTo(21.5852));
            Assert.That(location.Longitude, Is.EqualTo(105.8074));
        });
    }

    [TestCase(-90.1, 100)]
    [TestCase(90.1, 100)]
    [TestCase(20, -180.1)]
    [TestCase(20, 180.1)]
    public void Constructor_WithInvalidCoordinates_ShouldThrowInvalidLocationException(double lat, double lon)
    {
        Assert.Throws<InvalidLocationException>(() => new GeoLocation(lat, lon));
    }

    [Test]
    public void DistanceToKm_SameLocation_ShouldBeZero()
    {
        var loc1 = new GeoLocation(21.5852, 105.8074);
        var loc2 = new GeoLocation(21.5852, 105.8074);

        var distance = loc1.DistanceToKm(loc2);

        Assert.That(distance, Is.EqualTo(0.0));
    }

    [Test]
    public void DistanceToKm_KnownDistance_ShouldBeAccurateWithinExpectedRange()
    {
        // ICTU Campus (Thai Nguyen)
        var ictu = new GeoLocation(21.5852, 105.8074);
        // Hanoi Hoan Kiem
        var hanoi = new GeoLocation(21.0285, 105.8542);

        var distance = ictu.DistanceToKm(hanoi);

        // Approximate straight-line distance is ~62 km (within 60 to 65 km)
        Assert.That(distance, Is.InRange(60.0, 65.0));
    }
}
