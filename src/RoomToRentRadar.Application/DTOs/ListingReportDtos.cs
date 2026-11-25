using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Application.DTOs;

public sealed record SubmitListingReportCommand(
    string RoomId,
    Guid ReporterUserId,
    ReportReason Reason,
    string? Note);

public sealed record ListingReportSubmittedDto(
    string ReportId,
    string RoomId,
    ReportStatus Status,
    DateTimeOffset CreatedAtUtc);

public sealed record GetModerationQueueQuery(
    ReportStatus? Status = ReportStatus.Pending,
    int Page = 1,
    int PageSize = 20);

public sealed record ModerationReportItemDto(
    string ReportId,
    string RoomId,
    string RoomTitleSnapshot,
    Guid? RoomLandlordId,
    Guid ReporterUserId,
    ReportReason Reason,
    string? Note,
    ReportStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAtUtc,
    string? ResolutionNote,
    string? ETag);

public sealed record ReviewListingReportCommand(
    string ReportId,
    string RoomId,
    Guid ReviewerUserId,
    ReportStatus TargetStatus,
    string? ResolutionNote,
    string? ExpectedETag = null);
