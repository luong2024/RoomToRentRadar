namespace RoomToRentRadar.Domain.Tests;

using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

[TestFixture]
public class RoomListingTests
{
    [Test]
    public void IsDisplayable_WhenAvailableAndVerified_ShouldReturnTrue()
    {
        var listing = CreateListing(RoomStatus.Available, VerificationStatus.Verified);

        Assert.That(listing.IsDisplayable, Is.True);
    }

    [TestCase(RoomStatus.Occupied, VerificationStatus.Verified)]
    [TestCase(RoomStatus.Reserved, VerificationStatus.Verified)]
    [TestCase(RoomStatus.Hidden, VerificationStatus.Verified)]
    [TestCase(RoomStatus.Available, VerificationStatus.Unverified)]
    [TestCase(RoomStatus.Available, VerificationStatus.Pending)]
    [TestCase(RoomStatus.Available, VerificationStatus.Rejected)]
    [TestCase(RoomStatus.Occupied, VerificationStatus.Unverified)]
    public void IsDisplayable_WhenNotAvailableOrNotVerified_ShouldReturnFalse(RoomStatus status, VerificationStatus verification)
    {
        var listing = CreateListing(status, verification);

        Assert.That(listing.IsDisplayable, Is.False);
    }

    [TestCase(0)]
    [TestCase(-10)]
    public void Constructor_WithInvalidArea_ShouldThrowArgumentOutOfRangeException(double area)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RoomListing(
                "room-1",
                "Cozy Room",
                "Desc",
                new Money(2000000),
                area,
                "123 Street",
                new GeoLocation(21.0, 105.0),
                RoomStatus.Available,
                VerificationStatus.Verified,
                "campus-1"));
    }

    private static RoomListing CreateListing(RoomStatus status, VerificationStatus verification)
    {
        return new RoomListing(
            "room-1",
            "Cozy Student Room",
            "A nice student room",
            new Money(1500000),
            20.0,
            "Z115 Street",
            new GeoLocation(21.5852, 105.8074),
            status,
            verification,
            "campus-ictu");
    }
}
