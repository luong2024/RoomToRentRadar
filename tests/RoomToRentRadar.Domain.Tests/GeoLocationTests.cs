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
    [TestCase(double.NaN, 100)]
    [TestCase(20, double.NaN)]
    [TestCase(double.PositiveInfinity, 100)]
    [TestCase(20, double.NegativeInfinity)]
    public void Constructor_WithInvalidCoordinates_ShouldThrowInvalidLocationException(double lat, double lon)
    {
        Assert.Throws<InvalidLocationException>(() => new GeoLocation(lat, lon));
    }

    [TestCase(0, 0)]
    [TestCase(-90, -180)]
    [TestCase(-90, 180)]
    [TestCase(90, -180)]
    [TestCase(90, 180)]
    public void FromOptional_AcceptsZeroAndInclusiveBounds(double latitude, double longitude)
    {
        Assert.That(GeoLocation.FromOptional(latitude, longitude), Is.EqualTo(new GeoLocation(latitude, longitude)));
    }

    [Test]
    public void FromOptional_DistinguishesAbsenceFromHalfPair()
    {
        Assert.That(GeoLocation.FromOptional(null, null), Is.Null);
        Assert.Throws<InvalidLocationException>(() => GeoLocation.FromOptional(null, 105.8));
        Assert.Throws<InvalidLocationException>(() => GeoLocation.FromOptional(21.5, null));
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
