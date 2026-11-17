namespace RoomToRentRadar.Domain.Enums;

/// <summary>
/// Represents the administrative verification status of a room listing.
/// </summary>
public enum VerificationStatus
{
    Unverified = 0,
    Pending = 1,
    Verified = 2,
    Rejected = 3
}
