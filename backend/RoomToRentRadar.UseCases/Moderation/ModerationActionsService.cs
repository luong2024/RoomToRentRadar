using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Models.Moderation;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Moderation;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.UseCases.Moderation;

/// <summary>
/// Claim, release and resolve act on the whole report group of a listing. Every
/// write runs in a short transaction with conditional updates (claim state and
/// listing <see cref="Listing.Version"/>); losing races get a conflict and roll back.
/// </summary>
public sealed class ModerationActionsService(
    IModerationActionRepository actionRepository,
    IUnitOfWork unitOfWork) : IModerationActionsUseCase
{
    public static readonly TimeSpan ClaimDuration = TimeSpan.FromMinutes(15);

    public async Task<ModerationActionOutcome> ClaimAsync(
        Guid reportId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var listingId = await actionRepository.FindActiveReportListingIdAsync(
            reportId, cancellationToken);
        if (listingId is null)
        {
            return ModerationActionOutcome.ReportNotFound;
        }

        if (await actionRepository.HasActiveClaimByOtherAsync(
                listingId.Value, actorId, now, cancellationToken))
        {
            return ModerationActionOutcome.ClaimConflict;
        }

        var expectedReports = await actionRepository.CountActiveReportsAsync(
            listingId.Value, cancellationToken);
        var claimedUntil = now.Add(ClaimDuration);

        var updated = await actionRepository.ClaimActiveReportsAsync(
            listingId.Value, actorId, now, claimedUntil, cancellationToken);
        if (updated != expectedReports)
        {
            return ModerationActionOutcome.ClaimConflict;
        }

        actionRepository.AddAuditLog(new ModerationAuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorId,
            Action = ModerationAuditAction.Claim,
            ListingId = listingId.Value,
            ReportId = reportId,
            CreatedAt = now
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ModerationActionOutcome.Success;
    }

    public async Task<ModerationActionOutcome> ReleaseAsync(
        Guid reportId,
        Guid actorId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var listingId = await actionRepository.FindActiveReportListingIdAsync(
            reportId, cancellationToken);
        if (listingId is null)
        {
            return ModerationActionOutcome.ReportNotFound;
        }

        var claimOwners = await actionRepository.GetActiveClaimOwnersAsync(
            listingId.Value, now, cancellationToken);
        if (claimOwners.Count == 0)
        {
            return ModerationActionOutcome.ClaimConflict;
        }

        if (!isAdmin && claimOwners.Any(owner => owner != actorId))
        {
            return ModerationActionOutcome.NotClaimOwner;
        }

        var released = await actionRepository.ReleaseActiveReportsAsync(
            listingId.Value, actorId, isAdmin, now, cancellationToken);
        if (released == 0)
        {
            return ModerationActionOutcome.ClaimConflict;
        }

        actionRepository.AddAuditLog(new ModerationAuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorId,
            Action = ModerationAuditAction.Release,
            ListingId = listingId.Value,
            ReportId = reportId,
            CreatedAt = now
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ModerationActionOutcome.Success;
    }

    public async Task<ModerationActionOutcome> ResolveAsync(
        Guid reportId,
        Guid actorId,
        bool isAdmin,
        ModerationResolutionAction action,
        string note,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var listingId = await actionRepository.FindReportListingIdAsync(
            reportId, cancellationToken);
        if (listingId is null)
        {
            return ModerationActionOutcome.ReportNotFound;
        }

        var activeStatuses = await actionRepository.GetActiveReportStatusesAsync(
            listingId.Value, cancellationToken);
        if (activeStatuses.Count == 0)
        {
            return ModerationActionOutcome.InvalidReportState;
        }

        // A single status identifies the group's state; a mix of open and
        // in-review reports has no single previous status to record.
        ListingReportStatus? previousReportStatus = activeStatuses.Count == 1
            ? activeStatuses[0]
            : null;

        var claimOwners = await actionRepository.GetActiveClaimOwnersAsync(
            listingId.Value, now, cancellationToken);
        if (!isAdmin)
        {
            if (claimOwners.Count == 0)
            {
                return ModerationActionOutcome.ClaimConflict;
            }

            if (claimOwners.Any(owner => owner != actorId))
            {
                return ModerationActionOutcome.NotClaimOwner;
            }
        }

        var listing = await actionRepository.GetListingStateAsync(
            listingId.Value, cancellationToken);
        if (listing is null)
        {
            return ModerationActionOutcome.ReportNotFound;
        }

        ListingStatus? nextListingStatus = null;
        switch (action)
        {
            case ModerationResolutionAction.HideListing:
                if (listing.Status == ListingStatus.Removed)
                {
                    return ModerationActionOutcome.InvalidListingTransition;
                }

                if (listing.Status != ListingStatus.Hidden)
                {
                    nextListingStatus = ListingStatus.Hidden;
                }

                break;
            case ModerationResolutionAction.RemoveListing:
                if (listing.Status != ListingStatus.Removed)
                {
                    nextListingStatus = ListingStatus.Removed;
                }

                break;
        }

        // Only a real status change touches the listing, and it must win the
        // version check; dismiss, warn_owner and no-op transitions leave the
        // listing untouched.
        if (nextListingStatus is { } status)
        {
            var listingUpdated = await actionRepository.TransitionListingAsync(
                listingId.Value, listing.Version, status, now, cancellationToken);
            if (listingUpdated == 0)
            {
                return ModerationActionOutcome.ResolutionConflict;
            }
        }

        var nextReportStatus = action == ModerationResolutionAction.Dismiss
            ? ListingReportStatus.Dismissed
            : ListingReportStatus.Resolved;

        var resolvedReports = await actionRepository.ResolveActiveReportsAsync(
            listingId.Value, nextReportStatus, cancellationToken);
        if (resolvedReports == 0)
        {
            return ModerationActionOutcome.ResolutionConflict;
        }

        actionRepository.AddAuditLog(new ModerationAuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorId,
            Action = ModerationAuditAction.Resolve,
            ResolutionAction = action,
            ListingId = listingId.Value,
            ReportId = reportId,
            PreviousReportStatus = previousReportStatus,
            NextReportStatus = nextReportStatus,
            Note = note,
            CreatedAt = now
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ModerationActionOutcome.Success;
    }

    public async Task<ModerationActionOutcome> ApproveListingAsync(
        Guid listingId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var listing = await actionRepository.GetListingStateAsync(listingId, cancellationToken);
        if (listing is null)
        {
            return ModerationActionOutcome.ListingNotFound;
        }

        if (listing.Status == ListingStatus.Published)
        {
            return ModerationActionOutcome.Success;
        }

        if (listing.Status != ListingStatus.PendingReview)
        {
            return ModerationActionOutcome.InvalidListingTransition;
        }

        var now = DateTime.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var listingUpdated = await actionRepository.TransitionListingAsync(
            listingId, listing.Version, ListingStatus.Published, now, cancellationToken);
        if (listingUpdated == 0)
        {
            return ModerationActionOutcome.ResolutionConflict;
        }

        actionRepository.AddAuditLog(new ModerationAuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorId,
            Action = ModerationAuditAction.Approve,
            ListingId = listingId,
            CreatedAt = now
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ModerationActionOutcome.Success;
    }
}
