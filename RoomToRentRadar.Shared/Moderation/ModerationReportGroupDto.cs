using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Shared.Moderation;

public sealed record ModerationReportGroupDto(
    Guid ListingId,
    string Title,
    ListingStatus Status,
    int TotalReports,
    IReadOnlyList<ModerationReasonCountDto> Reasons,
    DateTime OldestReportAt,
    Guid? ClaimedByUserId,
    DateTime? ClaimedUntilUtc,
    IReadOnlyList<Guid> ReportIds);
