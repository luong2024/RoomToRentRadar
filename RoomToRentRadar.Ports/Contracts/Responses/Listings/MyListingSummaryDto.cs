namespace RoomToRentRadar.Ports.Contracts.Responses.Listings;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>MyListingSummaryDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record MyListingSummaryDto(
    string Id,
    string Title,
    ListingStatus Status,
    string? StatusReason,
    bool NeedsReReview,
    bool IsVerified,
    string? CoverThumbUrl,
    long MonthlyRentVnd,
    DateTime LastUpdatedAtUtc,
    DateTime CreatedAtUtc
);