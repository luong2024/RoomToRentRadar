namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// Structured utility fees and documented estimate view per docs/api/openapi.yaml schema UtilitiesView.
/// </summary>
public sealed record UtilitiesView(
    decimal? ElectricityPriceVndPerKwh,
    decimal? WaterPriceVnd,
    string? WaterPriceUnit,
    decimal? InternetFeeVnd,
    decimal? ParkingFeeVnd,
    string? OtherFeesNote,
    long EstimatedMonthlyTotalVnd
);
