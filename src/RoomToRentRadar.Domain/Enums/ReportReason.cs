namespace RoomToRentRadar.Domain.Enums;

/// <summary>
/// Categorizes why a user reported a rental listing (US12).
/// </summary>
public enum ReportReason
{
    InaccurateInfo = 1,
    SuspectedScam = 2,
    AlreadyRented = 3,
    DuplicateListing = 4,
    InappropriateContent = 5,
    Other = 99
}
