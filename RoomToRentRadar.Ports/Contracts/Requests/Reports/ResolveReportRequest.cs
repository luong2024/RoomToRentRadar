namespace RoomToRentRadar.Ports.Contracts.Requests.Reports;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>ResolveReportRequest</c> in <c>docs/api/openapi.yaml</c> 1:1. suspendListing only with ActionTaken.</summary>
public sealed record ResolveReportRequest(
    ReportResolution Resolution,
    string Note,
    bool SuspendListing
);