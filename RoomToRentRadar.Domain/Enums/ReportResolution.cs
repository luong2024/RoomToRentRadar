namespace RoomToRentRadar.Domain.Enums;

/// <summary>Resolution chosen by an admin, matching the inline <c>resolution</c> enum of <c>ResolveReportRequest</c> in <c>docs/api/openapi.yaml</c>.</summary>
public enum ReportResolution
{
    Dismissed = 1,
    ActionTaken = 2
}