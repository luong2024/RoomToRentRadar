using Microsoft.AspNetCore.Http;
using RoomToRentRadar.Api.Features.Travel;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class TravelDestinationValidatorTests
{
    [Test]
    public void TryValidate_ValidCoordinates_ReturnsParsedValues()
    {
        var valid = TravelDestinationValidator.TryValidate(
            10.7769,
            106.7009,
            out var lat,
            out var lng,
            out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(lat, Is.EqualTo(10.7769));
            Assert.That(lng, Is.EqualTo(106.7009));
            Assert.That(problem, Is.Null);
        });
    }

    [TestCase(90d, 180d)]
    [TestCase(-90d, -180d)]
    [TestCase(0d, 0d)]
    public void TryValidate_BoundaryCoordinates_AreAccepted(double toLat, double toLng)
    {
        var valid = TravelDestinationValidator.TryValidate(toLat, toLng, out _, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(problem, Is.Null);
        });
    }

    [TestCase(null, 106.7)]
    [TestCase(10.7, null)]
    [TestCase(null, null)]
    public void TryValidate_MissingComponent_ReturnsInvalidDestination(double? toLat, double? toLng)
    {
        AssertInvalid(toLat, toLng);
    }

    [TestCase(double.NaN, 106.7)]
    [TestCase(10.7, double.NaN)]
    [TestCase(double.PositiveInfinity, 106.7)]
    [TestCase(10.7, double.NegativeInfinity)]
    public void TryValidate_NonFiniteComponent_ReturnsInvalidDestination(double toLat, double toLng)
    {
        AssertInvalid(toLat, toLng);
    }

    [TestCase(90.0001, 106.7)]
    [TestCase(-90.0001, 106.7)]
    [TestCase(10.7, 180.0001)]
    [TestCase(10.7, -180.0001)]
    public void TryValidate_OutOfRangeComponent_ReturnsInvalidDestination(double toLat, double toLng)
    {
        AssertInvalid(toLat, toLng);
    }

    private static void AssertInvalid(double? toLat, double? toLng)
    {
        var valid = TravelDestinationValidator.TryValidate(toLat, toLng, out _, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(
                problem.Extensions["code"],
                Is.EqualTo(TravelProblemFactory.InvalidDestination));
        });
    }
}
