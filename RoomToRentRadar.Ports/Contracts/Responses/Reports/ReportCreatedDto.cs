namespace RoomToRentRadar.Ports.Contracts.Responses.Reports;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>ReportCreatedDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record ReportCreatedDto(
    string Id,
    ReportStatus Status,
    DateTime CreatedAtUtc
);