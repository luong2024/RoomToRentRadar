namespace RoomToRentRadar.Ports.Contracts.Responses.Admin;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>ModerationRoomSummaryDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record ModerationRoomSummaryDto(
    string Id,
    string Title,
    ListingStatus Status,
    bool NeedsReReview,
    string? CoverThumbUrl,
    string LandlordId,
    string LandlordName,
    int OpenReportCount,
    DateTime CreatedAtUtc,
    DateTime LastUpdatedAtUtc
);