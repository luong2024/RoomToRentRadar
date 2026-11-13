namespace RoomToRentRadar.Domain.Tests;

using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using ListingRoomStatus = RoomToRentRadar.Domain.Enums.RoomStatus;

[TestFixture]
public class RoomListingTests
{
    [Test]
    public void IsDisplayable_WhenAvailableAndVerified_ShouldReturnTrue()
    {
        var listing = CreateListing(ListingRoomStatus.Available, VerificationStatus.Verified);

        Assert.That(listing.IsDisplayable, Is.True);
    }

    [TestCase(ListingRoomStatus.Occupied, VerificationStatus.Verified)]
    [TestCase(ListingRoomStatus.Reserved, VerificationStatus.Verified)]
    [TestCase(ListingRoomStatus.Hidden, VerificationStatus.Verified)]
    [TestCase(ListingRoomStatus.Available, VerificationStatus.Unverified)]
    [TestCase(ListingRoomStatus.Available, VerificationStatus.Pending)]
    [TestCase(ListingRoomStatus.Available, VerificationStatus.Rejected)]
    [TestCase(ListingRoomStatus.Occupied, VerificationStatus.Unverified)]
    public void IsDisplayable_WhenNotAvailableOrNotVerified_ShouldReturnFalse(ListingRoomStatus status, VerificationStatus verification)
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
                ListingRoomStatus.Available,
                VerificationStatus.Verified,
                "campus-1"));
    }

    private static RoomListing CreateListing(ListingRoomStatus status, VerificationStatus verification)
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
