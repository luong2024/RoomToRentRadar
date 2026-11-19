namespace RoomToRentRadar.Ports.Contracts.Responses.Reports;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>ReportDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record ReportDto(
    string Id,
    string RoomId,
    string RoomTitle,
    ListingStatus? RoomStatus,
    string ReporterId,
    string ReporterName,
    ReportReason Reason,
    string? Note,
    ReportStatus Status,
    bool IsHighPriority,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc,
    string? ResolutionNote
);