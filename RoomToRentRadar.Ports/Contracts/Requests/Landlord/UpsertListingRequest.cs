using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Ports.Contracts.Requests.Landlord;

/// <summary>Postal address of a listing (openapi <c>AddressDto</c>, ERD §4.4).</summary>
public sealed record AddressDto(string AddressLine, string Ward, string District = "TP. Thái Nguyên");

/// <summary>Pin location of a listing (openapi <c>GeoPointDto</c>, BR-LST-01).</summary>
public sealed record GeoPointDto(double Latitude, double Longitude);

/// <summary>Monthly utilities of a listing (openapi <c>UtilitiesDto</c>, BR-LST-03).</summary>
public sealed record UtilitiesDto(
    long? ElectricityPriceVndPerKwh,
    long? WaterPriceVnd,
    WaterPriceUnit? WaterPriceUnit,
    long? InternetFeeVnd,
    long? ParkingFeeVnd,
    string? OtherFeesNote);

/// <summary>House rules sent on create (openapi <c>HouseRulesFields</c>, BR-HR-01).</summary>
public sealed record HouseRulesFieldsDto(
    string? QuietHoursStart,
    string? QuietHoursEnd,
    string? CurfewTime,
    GuestPolicy GuestPolicy,
    string? GuestPolicyNote,
    CookingPolicy CookingPolicy,
    bool ParkingAvailable,
    string? ParkingRules,
    bool PetsAllowed,
    bool SmokingAllowed,
    string? OtherRules);

/// <summary>
/// Create/update payload of <c>createListing</c> / <c>updateListing</c> (openapi
/// <c>UpsertListingRequest</c>). Never contains status, isVerified, landlordId or timestamps
/// (GUARDRAILS §1.5.1).
/// </summary>
public sealed record UpsertListingRequest(
    string Title,
    string? Description,
    RoomType RoomType,
    long MonthlyRentVnd,
    long DepositVnd,
    double AreaM2,
    int? MaxOccupants,
    Furnishing? Furnishing,
    DateOnly? AvailableFrom,
    AddressDto Address,
    GeoPointDto Location,
    UtilitiesDto? Utilities,
    IReadOnlyList<Amenity>? Amenities,
    IReadOnlyList<SafetyFeature>? SafetyFeatures,
    IReadOnlyList<string> PhotoIds,
    HouseRulesFieldsDto? HouseRules);
