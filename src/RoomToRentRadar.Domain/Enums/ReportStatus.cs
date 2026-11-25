namespace RoomToRentRadar.Domain.Enums;

/// <summary>
/// Lifecycle states of a listing report in the moderation queue (US12).
/// </summary>
public enum ReportStatus
{
    Pending = 0,
    InReview = 1,
    Resolved = 2,
    Dismissed = 3
}
