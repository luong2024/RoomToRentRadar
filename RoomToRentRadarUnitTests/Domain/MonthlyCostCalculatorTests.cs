using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Services;

namespace RoomToRentRadarUnitTests.Domain;

[TestFixture]
public class MonthlyCostCalculatorTests
{
    [Test]
    public void EstimateMonthlyTotalVnd_WithPerCubicMeterWater_Adds4m3And100Kwh()
    {
        var total = MonthlyCostCalculator.EstimateMonthlyTotalVnd(
            monthlyRentVnd: 2_000_000L,
            electricityPriceVndPerKwh: 3_500L,
            waterPriceVnd: 25_000L,
            waterPriceUnit: WaterPriceUnit.PerCubicMeter,
            internetFeeVnd: 100_000L,
            parkingFeeVnd: 50_000L);

        // 2,000,000 + (100 * 3,500) + (4 * 25,000) + 100,000 + 50,000 = 2,600,000
        Assert.That(total, Is.EqualTo(2_600_000L));
    }

    [Test]
    public void EstimateMonthlyTotalVnd_WithPerPersonWater_AddsSinglePersonFee()
    {
        var total = MonthlyCostCalculator.EstimateMonthlyTotalVnd(
            monthlyRentVnd: 1_800_000L,
            electricityPriceVndPerKwh: 3_000L,
            waterPriceVnd: 80_000L,
            waterPriceUnit: WaterPriceUnit.PerPerson,
            internetFeeVnd: null,
            parkingFeeVnd: null);

        // 1,800,000 + 300,000 + 80,000 = 2,180,000
        Assert.That(total, Is.EqualTo(2_180_000L));
    }

    [Test]
    public void EstimateMonthlyTotalVnd_WithIncludedWaterAndNullUtilities_ReturnsRentOnly()
    {
        var total = MonthlyCostCalculator.EstimateMonthlyTotalVnd(
            monthlyRentVnd: 1_500_000L,
            electricityPriceVndPerKwh: null,
            waterPriceVnd: 100_000L,
            waterPriceUnit: WaterPriceUnit.Included,
            internetFeeVnd: null,
            parkingFeeVnd: null);

        Assert.That(total, Is.EqualTo(1_500_000L));
    }
}
