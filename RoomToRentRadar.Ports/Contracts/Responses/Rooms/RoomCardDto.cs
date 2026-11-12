namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// Lightweight card DTO shared with PR #16 foundation and aligned with docs/api/openapi.yaml schema RoomCardDto.
/// </summary>
public sealed record RoomCardDto(
    string Id,
    string Title,
    string Description,
    decimal Price,
    double Area,
    string Address,
    double Latitude,
    double Longitude,
    double DistanceKm,
    bool IsAvailable,
    bool IsVerified,
    DateTime CreatedAtUtc
)
{
    // Computed property aliases matching OpenAPI schema and existing callers
    public decimal MonthlyRent => Price;
    public decimal MonthlyRentVnd => Price;
    public double AreaSquareMeters => Area;
    public double AreaM2 => Area;
    public double DistanceToCampusKm => DistanceKm;
    public string ThumbnailUrl => string.Empty;
    public string AddressSummary => Address;
}
