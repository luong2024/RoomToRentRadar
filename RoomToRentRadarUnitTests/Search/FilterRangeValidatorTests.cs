using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Services.Search.Core;

namespace RoomToRentRadarUnitTests.Search;

[TestFixture]
public class FilterRangeValidatorTests
{
    [Test]
    public void Validate_DefaultCriteria_IsValid()
    {
        var result = FilterRangeValidator.Validate(null, null, null, null, null, null);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result, Is.EqualTo(FilterValidationResult.Valid));
    }

    [TestCase(-1L)]
    [TestCase(4_000_000L)]
    public void Validate_InvalidRent_ReturnsInvalidPriceRange(long minRent)
    {
        var result = FilterRangeValidator.Validate(minRent, 2_000_000, null, null, null, null);

        Assert.That(result.PriceErrorCode, Is.EqualTo(ProblemCodes.InvalidPriceRange));
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_NegativeMaxRent_ReturnsInvalidPriceRange()
    {
        var result = FilterRangeValidator.Validate(null, -500_000, null, null, null, null);

        Assert.That(result.PriceErrorCode, Is.EqualTo(ProblemCodes.InvalidPriceRange));
    }

    [Test]
    public void Validate_RentAboveSliderBound_IsStillValid()
    {
        var result = FilterRangeValidator.Validate(null, 12_000_000, null, null, null, null);

        Assert.That(result.IsValid, Is.True);
    }

    [TestCase(-1.0)]
    [TestCase(50.0)]
    public void Validate_InvalidArea_ReturnsInvalidAreaRange(double minArea)
    {
        var result = FilterRangeValidator.Validate(null, null, minArea, 10.0, null, null);

        Assert.That(result.AreaErrorCode, Is.EqualTo(ProblemCodes.InvalidAreaRange));
    }

    [Test]
    public void Validate_NaNOrInfinityArea_ReturnsInvalidAreaRange()
    {
        var nan = FilterRangeValidator.Validate(null, null, double.NaN, null, null, null);
        var infinity = FilterRangeValidator.Validate(null, null, null, double.PositiveInfinity, null, null);

        Assert.That(nan.AreaErrorCode, Is.EqualTo(ProblemCodes.InvalidAreaRange));
        Assert.That(infinity.AreaErrorCode, Is.EqualTo(ProblemCodes.InvalidAreaRange));
    }

    [TestCase(0.05)]
    [TestCase(51.0)]
    public void Validate_DistanceOutOfRange_ReturnsInvalidDistanceRange(double km)
    {
        var result = FilterRangeValidator.Validate(null, null, null, null, km, null);

        Assert.That(result.DistanceErrorCode, Is.EqualTo(ProblemCodes.InvalidDistanceRange));
    }

    [Test]
    public void Validate_AllowedDistanceOptions_AreValid()
    {
        foreach (var km in FilterRangeValidator.DistanceOptions)
        {
            var result = FilterRangeValidator.Validate(null, null, null, null, km, null);
            Assert.That(result.DistanceErrorCode, Is.Null, $"Distance {km} km should be valid.");
        }
    }

    [Test]
    public void Validate_UnknownSort_ReturnsInvalidSortOption()
    {
        var result = FilterRangeValidator.Validate(null, null, null, null, null, "cheapest");

        Assert.That(result.SortErrorCode, Is.EqualTo(ProblemCodes.InvalidSortOption));
    }

    [TestCase("distance")]
    [TestCase("price_asc")]
    [TestCase("price_desc")]
    [TestCase("newest")]
    public void Validate_WhitelistedSort_IsValid(string sort)
    {
        var result = FilterRangeValidator.Validate(null, null, null, null, null, sort);

        Assert.That(result.SortErrorCode, Is.Null);
    }
}
