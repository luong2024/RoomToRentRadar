using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;

namespace RoomToRentRadar.Ports.Contracts.Responses.Landlord;

/// <summary>
/// Owner view of one listing (openapi <c>MyListingDetailDto</c>, UC-30/UC-32/UC-33).
/// <see cref="Listing"/> mirrors the <c>listing</c> property of the OpenAPI schema exactly, so the
/// same <see cref="UpsertListingRequest"/> model is reused instead of duplicating it.
/// <see cref="Version"/> is also the <c>ETag</c> value (ADR-012).
/// </summary>
public sealed record MyListingDetailDto(
    string Id,
    ListingStatus Status,
    string? StatusReason,
    bool NeedsReReview,
    bool IsVerified,
    bool IsSafetyVerified,
    int Version,
    UpsertListingRequest Listing,
    IReadOnlyList<PhotoDto> Photos,
    HouseRulesDto? HouseRules,
    DateTime? PublishedAtUtc,
    DateTime LastUpdatedAtUtc,
    DateTime CreatedAtUtc);
