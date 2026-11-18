namespace RoomToRentRadar.Domain.Enums;

/// <summary>Reasons for reporting a room, matching <c>ReportReason</c> in <c>docs/api/openapi.yaml</c>.</summary>
public enum ReportReason
{
    Scam = 1,
    IncorrectInformation = 2,
    AlreadyRented = 3,
    Duplicate = 4,
    InappropriateContent = 5,
    Other = 6
}