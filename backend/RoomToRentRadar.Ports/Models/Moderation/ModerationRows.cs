using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.Ports.Models.Moderation;

/// <summary>Row shape of the grouped queue page; matched to the raw SQL aliases.</summary>
public sealed class ModerationGroupRow
{
    public Guid ListingId { get; set; }
    public int TotalReports { get; set; }
    public DateTime OldestReportAt { get; set; }
}

public sealed record ModerationListingSummary(Guid Id, string Title, ListingStatus Status);

public sealed record ModerationReasonCountRow(
    Guid ListingId,
    ListingReportReason Reason,
    int Count);

public sealed record ModerationActiveClaim(Guid ListingId, Guid ClaimedByUserId, DateTime ClaimedUntilUtc);

public sealed record ModerationListingState(long Version, ListingStatus Status);
