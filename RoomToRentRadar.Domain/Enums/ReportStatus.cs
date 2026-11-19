namespace RoomToRentRadar.Domain.Enums;

/// <summary>Lifecycle states of a report, matching <c>ReportStatus</c> in <c>docs/api/openapi.yaml</c>.</summary>
public enum ReportStatus
{
    Open = 1,
    Dismissed = 2,
    ActionTaken = 3
}