using RoomToRentRadar.Shared.Moderation;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.Api.Domain;

public sealed class ModerationAuditLog
{
    public Guid Id { get; set; }
    public Guid ActorUserId { get; set; }
    public ModerationAuditAction Action { get; set; }
    public ModerationResolutionAction? ResolutionAction { get; set; }
    public Guid ListingId { get; set; }
    public Guid ReportId { get; set; }
    public ListingReportStatus? PreviousReportStatus { get; set; }
    public ListingReportStatus? NextReportStatus { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
