using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.Shared.Moderation;

public sealed record ModerationReasonCountDto(ListingReportReason Reason, int Count);
