using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// Authoritative full room detail response DTO per docs/api/openapi.yaml schema RoomDetailDto.
/// Conforms to US02, US03, US11, and BR-AUTH-07 phone gating.
/// All field names are canonical OpenAPI names — legacy aliases have been removed per M3 fix.
/// </summary>
public sealed record RoomDetailDto(
    string Id,
    string Title,
    string? Description,
    RoomType RoomType,
    ListingStatus Status,
    bool IsVerified,
    decimal MonthlyRentVnd,
    decimal DepositVnd,
    double AreaM2,
    AddressDto Address,
    GeoPointDto Location,
    double DistanceKm,
    UtilitiesView Utilities,
    IReadOnlyList<Amenity> Amenities,
    SafetyDto Safety,
    IReadOnlyList<PhotoDto> Photos,
    LandlordContactDto Landlord,
    DateTime LastUpdatedAtUtc,
    HouseRulesDto? HouseRules = null,
    int? MaxOccupants = null,
    string? Furnishing = null,
    string? AvailableFrom = null,
    bool IsFavourite = false,
    DateTime? PublishedAtUtc = null
);
