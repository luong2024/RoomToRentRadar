namespace RoomToRentRadar.Domain.Enums;

/// <summary>Push notification kinds, matching <c>NotificationType</c> in <c>docs/api/openapi.yaml</c>.</summary>
public enum NotificationType
{
    NewListingMatch = 1,
    ListingApproved = 2,
    ListingRejected = 3,
    ListingSuspended = 4,
    LandlordApproved = 5,
    ReportResolved = 6,
    NewMessage = 7
}