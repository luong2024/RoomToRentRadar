namespace RoomToRentRadar.Api.Features.Moderation;

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
