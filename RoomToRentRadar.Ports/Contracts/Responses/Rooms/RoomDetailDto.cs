namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;

/// <summary>Matches <c>RoomDetailDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record RoomDetailDto(
    string Id,
    string Title,
    string? Description,
    RoomType RoomType,
    ListingStatus Status,
    bool IsVerified,
    long MonthlyRentVnd,
    long DepositVnd,
    double AreaM2,
    int? MaxOccupants,
    Furnishing Furnishing,
    DateOnly? AvailableFrom,
    AddressDto Address,
    GeoPointDto Location,
    double DistanceKm,
    UtilitiesView Utilities,
    IReadOnlyList<Amenity> Amenities,
    SafetyDto Safety,
    HouseRulesDto? HouseRules,
    IReadOnlyList<PhotoDto> Photos,
    LandlordContactDto Landlord,
    bool IsFavourite,
    DateTime? PublishedAtUtc,
    DateTime LastUpdatedAtUtc
);