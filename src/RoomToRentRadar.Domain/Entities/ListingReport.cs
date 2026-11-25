using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Entities;

/// <summary>
/// Aggregate Root representing a user-submitted report against a rental listing (US12).
/// </summary>
public sealed class ListingReport
{
    public const int MaxNoteLength = 500;
    public const int MaxResolutionNoteLength = 500;

    private static readonly IReadOnlySet<ReportReason> ReasonsRequiringNote = new HashSet<ReportReason>
    {
        ReportReason.Other
    };

    private static readonly IReadOnlyDictionary<ReportStatus, IReadOnlySet<ReportStatus>> AllowedTransitions =
        new Dictionary<ReportStatus, IReadOnlySet<ReportStatus>>
        {
            [ReportStatus.Pending] = new HashSet<ReportStatus> { ReportStatus.InReview, ReportStatus.Resolved, ReportStatus.Dismissed },
            [ReportStatus.InReview] = new HashSet<ReportStatus> { ReportStatus.Resolved, ReportStatus.Dismissed },
            [ReportStatus.Resolved] = new HashSet<ReportStatus>(),
            [ReportStatus.Dismissed] = new HashSet<ReportStatus>()
        };

    public string Id { get; }
    public string RoomId { get; }
    public string RoomTitleSnapshot { get; }
    public Guid? RoomLandlordId { get; }
    public Guid ReporterUserId { get; }
    public ReportReason Reason { get; }
    public string? Note { get; }
    public ReportStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public string? ResolutionNote { get; private set; }
    public string? ETag { get; private set; }

    private ListingReport(
        string id,
        string roomId,
        string roomTitleSnapshot,
        Guid? roomLandlordId,
        Guid reporterUserId,
        ReportReason reason,
        string? note,
        ReportStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        Guid? reviewedByUserId,
        DateTimeOffset? reviewedAtUtc,
        string? resolutionNote,
        string? etag)
    {
        Id = id;
        RoomId = roomId;
        RoomTitleSnapshot = roomTitleSnapshot;
        RoomLandlordId = roomLandlordId;
        ReporterUserId = reporterUserId;
        Reason = reason;
        Note = note;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAtUtc = reviewedAtUtc;
        ResolutionNote = resolutionNote;
        ETag = etag;
    }

    public static ListingReport Create(
        string roomId,
        string roomTitleSnapshot,
        Guid? roomLandlordId,
        Guid reporterUserId,
        ReportReason reason,
        string? note,
        DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new ReportDomainException("Room identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(roomTitleSnapshot))
        {
            throw new ReportDomainException("Room title snapshot is required.");
        }

        if (reporterUserId == Guid.Empty)
        {
            throw new ReportDomainException("Reporter user identifier is required.");
        }

        if (!Enum.IsDefined(reason))
        {
            throw new ReportDomainException("Selected report reason is not supported.");
        }

        var normalizedNote = NormalizeOptionalText(note, MaxNoteLength, "Report note");
        if (ReasonsRequiringNote.Contains(reason) && string.IsNullOrWhiteSpace(normalizedNote))
        {
            throw new ReportDomainException("A note is required when selecting 'Other' as the report reason.");
        }

        return new ListingReport(
            id: $"report:{Guid.NewGuid():N}",
            roomId: roomId.Trim(),
            roomTitleSnapshot: roomTitleSnapshot.Trim(),
            roomLandlordId: roomLandlordId,
            reporterUserId: reporterUserId,
            reason: reason,
            note: normalizedNote,
            status: ReportStatus.Pending,
            createdAtUtc: utcNow,
            updatedAtUtc: utcNow,
            reviewedByUserId: null,
            reviewedAtUtc: null,
            resolutionNote: null,
            etag: null);
    }

    public static ListingReport Rehydrate(
        string id,
        string roomId,
        string roomTitleSnapshot,
        Guid? roomLandlordId,
        Guid reporterUserId,
        ReportReason reason,
        string? note,
        ReportStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        Guid? reviewedByUserId,
        DateTimeOffset? reviewedAtUtc,
        string? resolutionNote,
        string? etag = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ReportDomainException("Report identifier is required.");
        }

        return new ListingReport(
            id,
            roomId,
            roomTitleSnapshot,
            roomLandlordId,
            reporterUserId,
            reason,
            note,
            status,
            createdAtUtc,
            updatedAtUtc,
            reviewedByUserId,
            reviewedAtUtc,
            resolutionNote,
            etag);
    }

    public void ApplyReviewDecision(
        ReportStatus targetStatus,
        Guid reviewerUserId,
        string? resolutionNote,
        DateTimeOffset utcNow)
    {
        if (reviewerUserId == Guid.Empty)
        {
            throw new ReportDomainException("Reviewer user identifier is required.");
        }

        if (!AllowedTransitions.TryGetValue(Status, out var validTargets) || !validTargets.Contains(targetStatus))
        {
            throw new ReportDomainException($"Cannot transition listing report from '{Status}' to '{targetStatus}'.");
        }

        var normalizedResolutionNote = NormalizeOptionalText(resolutionNote, MaxResolutionNoteLength, "Resolution note");
        if (targetStatus is ReportStatus.Resolved or ReportStatus.Dismissed && string.IsNullOrWhiteSpace(normalizedResolutionNote))
        {
            throw new ReportDomainException($"A resolution note is required when marking a report as '{targetStatus}'.");
        }

        Status = targetStatus;
        ReviewedByUserId = reviewerUserId;
        ReviewedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
        ResolutionNote = normalizedResolutionNote;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ReportDomainException($"{fieldName} must not exceed {maxLength} characters.");
        }

        return trimmed;
    }
}
