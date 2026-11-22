using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.Domain.Entities;

public sealed class ListingReport
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }
    public Guid ReporterId { get; set; }
    public ListingReportReason Reason { get; set; }
    public string? Details { get; set; }
    public ListingReportStatus Status { get; set; }
    public Guid? ClaimedByUserId { get; set; }
    public DateTime? ClaimedUntilUtc { get; set; }
    public DateTime CreatedAt { get; set; }
    public long Version { get; set; }
}
