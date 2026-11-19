namespace RoomToRentRadar.Domain.Enums;

/// <summary>BR-LST-04 / ADR-011: lifecycle states of a listing.</summary>
public enum ListingStatus
{
    PendingReview = 1,
    Rejected = 2,
    Available = 3,
    Rented = 4,
    Unpublished = 5,
    Suspended = 6
}
