using RoomToRentRadar.Services;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class RoomSecurityScoreTests
{
    [Test]
    public void Calculate_WithNoAmenities_ReturnsBaseScoreNeedingCaution()
    {
        var result = RoomSecurityScore.Calculate([]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Score, Is.EqualTo(40));
            Assert.That(result.Band, Is.EqualTo("Cần cân nhắc"));
        });
    }

    [Test]
    public void Calculate_WithSecurityAmenity_AddsTwentyFivePoints()
    {
        var result = RoomSecurityScore.Calculate([AmenityCode.Security]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Score, Is.EqualTo(65));
            Assert.That(result.Band, Is.EqualTo("An toàn"));
        });
    }

    [Test]
    public void Calculate_WithAllSafetyAmenities_ReachesMaximumScore()
    {
        var result = RoomSecurityScore.Calculate(
        [
            AmenityCode.Security,
            AmenityCode.Parking,
            AmenityCode.Elevator,
            AmenityCode.PrivateBathroom,
            AmenityCode.Balcony
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Score, Is.EqualTo(100));
            Assert.That(result.Band, Is.EqualTo("Rất an toàn"));
        });
    }

    [Test]
    public void Calculate_WithParkingOnly_ReturnsKháBand()
    {
        var result = RoomSecurityScore.Calculate([AmenityCode.Parking]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Score, Is.EqualTo(52));
            Assert.That(result.Band, Is.EqualTo("Khá"));
        });
    }

    [Test]
    public void Calculate_WithNonSafetyAmenities_KeepsBaseScore()
    {
        var result = RoomSecurityScore.Calculate(
        [
            AmenityCode.Wifi,
            AmenityCode.AirConditioner,
            AmenityCode.Gym,
            AmenityCode.Pool,
            AmenityCode.PetAllowed
        ]);

        Assert.That(result.Score, Is.EqualTo(40));
    }

    [Test]
    public void Calculate_WithDuplicateAmenities_DoesNotDoubleCount()
    {
        var result = RoomSecurityScore.Calculate([AmenityCode.Security, AmenityCode.Security]);

        Assert.That(result.Score, Is.EqualTo(65));
    }

    [Test]
    public void Calculate_WithNullAmenities_ReturnsBaseScore()
    {
        var result = RoomSecurityScore.Calculate(null);

        Assert.That(result.Score, Is.EqualTo(40));
    }
}
