namespace RoomToRentRadar.Ports.Contracts.Requests.Listings;

using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// Matches <c>UpsertListingRequest</c> in <c>docs/api/openapi.yaml</c> 1:1. Create/update payload; never carries
/// status, isVerified, landlordId or timestamps (GUARDRAILS 1.5.1). houseRules is ignored by updateListing (BR-HR-02).
/// </summary>
public sealed record UpsertListingRequest(
    string Title,
    string? Description,
    RoomType RoomType,
    long MonthlyRentVnd,
    long DepositVnd,
    double AreaM2,
    int? MaxOccupants,
    Furnishing Furnishing,
    DateOnly? AvailableFrom,
    AddressDto Address,
    GeoPointDto Location,
    UtilitiesDto? Utilities,
    IReadOnlyList<Amenity>? Amenities,
    IReadOnlyList<SafetyFeature>? SafetyFeatures,
    IReadOnlyList<string> PhotoIds,
    HouseRulesFields? HouseRules
);