using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;

public sealed record SubmitListingReportRequest(
    ReportReason Reason,
    string? Note = null);

public sealed record ReviewListingReportRequest(
    string RoomId,
    ReportStatus Status,
    string? ResolutionNote = null,
    string? ExpectedETag = null);
