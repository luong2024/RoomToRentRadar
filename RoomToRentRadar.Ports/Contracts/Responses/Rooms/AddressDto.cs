namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// Address DTO per docs/api/openapi.yaml schema AddressDto.
/// </summary>
public sealed record AddressDto(
    string AddressLine,
    string Ward,
    string District = "TP. Thái Nguyên"
);
