namespace RoomToRentRadar.Domain.Enums;

/// <summary>How the water price is charged, matching <c>WaterPriceUnit</c> in <c>docs/api/openapi.yaml</c>.</summary>
public enum WaterPriceUnit
{
    PerCubicMeter = 1,
    PerPerson = 2,
    Included = 3
}
