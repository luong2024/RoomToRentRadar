namespace RoomToRentRadar.Api.Features.Moderation;

public enum ModerationActionOutcome
{
    Success,
    ReportNotFound,
    ClaimConflict,
    NotClaimOwner,
    InvalidReportState,
    InvalidListingTransition,
    ResolutionConflict
}
