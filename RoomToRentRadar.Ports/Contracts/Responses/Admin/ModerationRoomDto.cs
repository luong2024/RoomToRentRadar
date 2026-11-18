namespace RoomToRentRadar.Ports.Contracts.Responses.Admin;

using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Listings;
using RoomToRentRadar.Ports.Contracts.Responses.Listings;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>Matches <c>ModerationRoomDto</c> in <c>docs/api/openapi.yaml</c> 1:1 (MyListingDetailDto + landlord).</summary>
public sealed record ModerationRoomDto(
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
    DateTime CreatedAtUtc,
    ModerationLandlordDto Landlord
) : MyListingDetailDto(Id, Status, StatusReason, NeedsReReview, IsVerified, IsSafetyVerified, Version, Listing, Photos, HouseRules, PublishedAtUtc, LastUpdatedAtUtc, CreatedAtUtc);

/// <summary>The inline <c>landlord</c> object of <c>ModerationRoomDto</c> in <c>docs/api/openapi.yaml</c>.</summary>
public sealed record ModerationLandlordDto(
    string Id,
    string FullName,
    string Email,
    string PhoneNumber,
    bool IsVerified
);