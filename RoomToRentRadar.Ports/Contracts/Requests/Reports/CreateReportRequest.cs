namespace RoomToRentRadar.Ports.Contracts.Requests.Reports;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>CreateReportRequest</c> in <c>docs/api/openapi.yaml</c> 1:1. Note is required when reason = Other.</summary>
public sealed record CreateReportRequest(
    ReportReason Reason,
    string? Note
);