using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// Authoritative full room detail response DTO per docs/api/openapi.yaml schema RoomDetailDto.
/// Conforms to US02, US03, US11, and BR-AUTH-07 phone gating.
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
)
{
    // Convenience & Compatibility accessors
    public decimal MonthlyRent => MonthlyRentVnd;
    public decimal DepositAmount => DepositVnd;
    public double AreaSquareMeters => AreaM2;
    public string FullAddress => Address != null
        ? $"{Address.AddressLine}, {Address.Ward}, {Address.District}".Trim(',', ' ')
        : string.Empty;
    public string LandlordName => Landlord?.DisplayName ?? string.Empty;
    public string? LandlordPhoneNumber => Landlord?.PhoneNumber;
    public bool IsAvailable => Status == ListingStatus.Available;
    public DateTime LastUpdatedAt => LastUpdatedAtUtc;
    public IReadOnlyList<string> PhotoUrls => Photos != null ? Photos.Select(p => p.MediumUrl).ToList() : Array.Empty<string>();
    public string? Panorama360Url => Photos?.FirstOrDefault(p => p.Kind == "Panorama")?.OriginalUrl;

    /// <summary>
    /// Deprecated in API response body per review H2 (client displays banner by status).
    /// Preserved as computed helper for transitional tests.
    /// </summary>
    public string? UnavailableMessage => !IsAvailable
        ? "Phòng trọ này hiện không còn trống."
        : null;

    /// <summary>
    /// Similar rooms are separated into GET /api/rooms/{roomId}/similar per Review H2 & BR-LST-10.
    /// </summary>
    public IReadOnlyList<RoomCardDto> SimilarRooms => Array.Empty<RoomCardDto>();
}
