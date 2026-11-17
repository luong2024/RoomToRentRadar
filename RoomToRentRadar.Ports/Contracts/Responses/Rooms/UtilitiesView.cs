namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>UtilitiesView</c> in <c>docs/api/openapi.yaml</c> 1:1 (UtilitiesDto + estimated monthly total).</summary>
public sealed record UtilitiesView(
    long? ElectricityPriceVndPerKwh,
    long? WaterPriceVnd,
    WaterPriceUnit WaterPriceUnit,
    long? InternetFeeVnd,
    long? ParkingFeeVnd,
    string? OtherFeesNote,
    long EstimatedMonthlyTotalVnd
);