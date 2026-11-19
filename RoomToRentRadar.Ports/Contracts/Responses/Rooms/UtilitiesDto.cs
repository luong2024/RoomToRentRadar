namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>UtilitiesDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record UtilitiesDto(
    long? ElectricityPriceVndPerKwh,
    long? WaterPriceVnd,
    WaterPriceUnit WaterPriceUnit,
    long? InternetFeeVnd,
    long? ParkingFeeVnd,
    string? OtherFeesNote
);