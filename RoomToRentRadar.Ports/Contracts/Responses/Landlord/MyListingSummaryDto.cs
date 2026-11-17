using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Ports.Contracts.Responses.Landlord;

/// <summary>Row of <c>operationId: listMyListings</c> (openapi <c>MyListingSummaryDto</c>, UC-31).</summary>
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
    DateTime CreatedAtUtc);
