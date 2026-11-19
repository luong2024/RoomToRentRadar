namespace RoomToRentRadar.Ports.Contracts.Responses.Listings;

using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Listings;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// Matches <c>MyListingDetailDto</c> in <c>docs/api/openapi.yaml</c> 1:1. Not sealed so <c>ModerationRoomDto</c>
/// can extend it with the landlord block (allOf). <c>Version</c> is the ETag value passed as expectedVersion.
/// </summary>
public record MyListingDetailDto(
    string Id,
    ListingStatus Status,
    string? StatusReason,
    bool NeedsReReview,
    bool IsVerified,
    bool IsSafetyVerified,
    long Version,
    UpsertListingRequest Listing,
    IReadOnlyList<PhotoDto> Photos,
    HouseRulesDto? HouseRules,
    DateTime? PublishedAtUtc,
    DateTime LastUpdatedAtUtc,
    DateTime CreatedAtUtc
);