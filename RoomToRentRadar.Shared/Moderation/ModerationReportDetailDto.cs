using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.Shared.Moderation;

/// <summary>Single report with the sanitized details a moderator needs to decide.</summary>
public sealed record ModerationReportDetailDto(
    Guid Id,
    Guid ListingId,
    string ListingTitle,
    ListingStatus ListingStatus,
    Guid ReporterId,
    ListingReportReason Reason,
    string? Details,
    ListingReportStatus Status,
    Guid? ClaimedByUserId,
    DateTime? ClaimedUntilUtc,
    DateTime CreatedAt);
