namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// Structured utility fees and documented estimate view per docs/api/openapi.yaml schema UtilitiesView.
/// </summary>
public sealed record UtilitiesView(
    long? ElectricityPriceVndPerKwh,
    long? WaterPriceVnd,
    string? WaterPriceUnit,
    long? InternetFeeVnd,
    long? ParkingFeeVnd,
    string? OtherFeesNote,
    long EstimatedMonthlyTotalVnd
);
