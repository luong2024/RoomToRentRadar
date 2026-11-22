namespace RoomToRentRadar.Ports.Models.Moderation;

public enum ModerationActionOutcome
{
    Success,
    ReportNotFound,
    ListingNotFound,
    ClaimConflict,
    NotClaimOwner,
    InvalidReportState,
    InvalidListingTransition,
    ResolutionConflict
}
