using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Domain.Services;

/// <summary>
/// Pure domain calculation for estimated monthly room cost (BR-LST-03):
/// monthly rent + 100 kWh electricity + water (4 m³, 1 × per person, or 0 if included) + internet + parking.
/// </summary>
public static class MonthlyCostCalculator
{
    public const long AssumedMonthlyElectricityKwh = 100L;
    public const long AssumedMonthlyWaterCubicMeters = 4L;

    public static long EstimateMonthlyTotalVnd(
        long monthlyRentVnd,
        long? electricityPriceVndPerKwh,
        long? waterPriceVnd,
        WaterPriceUnit waterPriceUnit,
        long? internetFeeVnd,
        long? parkingFeeVnd)
    {
        var total = monthlyRentVnd;

        if (electricityPriceVndPerKwh.HasValue)
        {
            total += AssumedMonthlyElectricityKwh * electricityPriceVndPerKwh.Value;
        }

        if (waterPriceVnd.HasValue)
        {
            total += waterPriceUnit switch
            {
                WaterPriceUnit.PerPerson => waterPriceVnd.Value,
                WaterPriceUnit.Included => 0L,
                _ => AssumedMonthlyWaterCubicMeters * waterPriceVnd.Value
            };
        }

        if (internetFeeVnd.HasValue)
        {
            total += internetFeeVnd.Value;
        }

        if (parkingFeeVnd.HasValue)
        {
            total += parkingFeeVnd.Value;
        }

        return total;
    }
}
