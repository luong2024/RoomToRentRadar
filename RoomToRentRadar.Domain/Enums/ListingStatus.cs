namespace RoomToRentRadar.Domain.Enums;

/// <summary>
/// Authoritative listing lifecycle status per docs/erd.md §4.4, docs/business-analysis.md BR-LST-04, and ADR-011.
/// </summary>
public enum ListingStatus
{
    PendingReview = 0,
    Rejected = 1,
    Available = 2,
    Rented = 3,
    Unpublished = 4,
    Suspended = 5
}
