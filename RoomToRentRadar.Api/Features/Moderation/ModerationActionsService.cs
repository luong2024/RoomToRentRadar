using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Moderation;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.Api.Features.Moderation;

/// <summary>
/// Claim, release and resolve act on the whole report group of a listing. Every
/// write runs in a short SQLite transaction with conditional updates (claim state
/// and <see cref="Listing.Version"/>); losing races get a conflict and roll back.
/// </summary>
public sealed class ModerationActionsService(AppDbContext dbContext)
{
    public static readonly TimeSpan ClaimDuration = TimeSpan.FromMinutes(15);

    public async Task<ModerationActionOutcome> ClaimAsync(
        Guid reportId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var listingId = await FindActiveListingIdAsync(reportId, cancellationToken);
        if (listingId is null)
        {
            return ModerationActionOutcome.ReportNotFound;
        }

        var claimedByOther = await ActiveReports(listingId.Value)
            .AsNoTracking()
            .AnyAsync(
                report => report.ClaimedByUserId != null
                    && report.ClaimedByUserId != actorId
                    && report.ClaimedUntilUtc > now,
                cancellationToken);
        if (claimedByOther)
        {
            return ModerationActionOutcome.ClaimConflict;
        }

        var expectedReports = await ActiveReports(listingId.Value).CountAsync(cancellationToken);
        var claimedUntil = now.Add(ClaimDuration);

        var updated = await ActiveReports(listingId.Value)
            .Where(report => report.ClaimedByUserId == null
                || report.ClaimedByUserId == actorId
                || report.ClaimedUntilUtc == null
                || report.ClaimedUntilUtc <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(report => report.Status, ListingReportStatus.InReview)
                    .SetProperty(report => report.ClaimedByUserId, actorId)
                    .SetProperty(report => report.ClaimedUntilUtc, claimedUntil)
                    .SetProperty(report => report.Version, report => report.Version + 1),
                cancellationToken);
        if (updated != expectedReports)
        {
            return ModerationActionOutcome.ClaimConflict;
        }

        dbContext.ModerationAuditLogs.Add(new ModerationAuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorId,
            Action = ModerationAuditAction.Claim,
            ListingId = listingId.Value,
            ReportId = reportId,
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
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
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var listingId = await FindActiveListingIdAsync(reportId, cancellationToken);
        if (listingId is null)
        {
            return ModerationActionOutcome.ReportNotFound;
        }

        var claimOwners = await ActiveReports(listingId.Value)
            .AsNoTracking()
            .Where(report => report.ClaimedByUserId != null && report.ClaimedUntilUtc > now)
            .Select(report => report.ClaimedByUserId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (claimOwners.Count == 0)
        {
            return ModerationActionOutcome.ClaimConflict;
        }

        if (!isAdmin && claimOwners.Any(owner => owner != actorId))
        {
            return ModerationActionOutcome.NotClaimOwner;
        }

        var released = await ActiveReports(listingId.Value)
            .Where(report => report.ClaimedByUserId != null
                && report.ClaimedUntilUtc > now
                && (isAdmin || report.ClaimedByUserId == actorId))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(report => report.ClaimedByUserId, (Guid?)null)
                    .SetProperty(report => report.ClaimedUntilUtc, (DateTime?)null)
                    .SetProperty(report => report.Version, report => report.Version + 1),
                cancellationToken);
        if (released == 0)
        {
            return ModerationActionOutcome.ClaimConflict;
        }

        dbContext.ModerationAuditLogs.Add(new ModerationAuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorId,
            Action = ModerationAuditAction.Release,
            ListingId = listingId.Value,
            ReportId = reportId,
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
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
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var listingId = await FindAnyListingIdAsync(reportId, cancellationToken);
        if (listingId is null)
        {
            return ModerationActionOutcome.ReportNotFound;
        }

        var activeReports = ActiveReports(listingId.Value);
        var activeStatuses = await activeReports
            .AsNoTracking()
            .Select(report => report.Status)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (activeStatuses.Count == 0)
        {
            return ModerationActionOutcome.InvalidReportState;
        }

        // A single status identifies the group's state; a mix of open and
        // in-review reports has no single previous status to record.
        ListingReportStatus? previousReportStatus = activeStatuses.Count == 1
            ? activeStatuses[0]
            : null;

        var claimOwners = await activeReports
            .AsNoTracking()
            .Where(report => report.ClaimedByUserId != null && report.ClaimedUntilUtc > now)
            .Select(report => report.ClaimedByUserId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
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

        var listing = await dbContext.Listings
            .AsNoTracking()
            .Where(candidate => candidate.Id == listingId.Value)
            .Select(candidate => new { candidate.Version, candidate.Status })
            .FirstOrDefaultAsync(cancellationToken);
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
            var listingUpdated = await dbContext.Listings
                .Where(candidate => candidate.Id == listingId.Value && candidate.Version == listing.Version)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(candidate => candidate.Status, status)
                        .SetProperty(candidate => candidate.UpdatedAt, now)
                        .SetProperty(candidate => candidate.Version, candidate => candidate.Version + 1),
                    cancellationToken);
            if (listingUpdated == 0)
            {
                return ModerationActionOutcome.ResolutionConflict;
            }
        }

        var nextReportStatus = action == ModerationResolutionAction.Dismiss
            ? ListingReportStatus.Dismissed
            : ListingReportStatus.Resolved;

        var resolvedReports = await ActiveReports(listingId.Value)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(report => report.Status, nextReportStatus)
                    .SetProperty(report => report.ClaimedByUserId, (Guid?)null)
                    .SetProperty(report => report.ClaimedUntilUtc, (DateTime?)null)
                    .SetProperty(report => report.Version, report => report.Version + 1),
                cancellationToken);
        if (resolvedReports == 0)
        {
            return ModerationActionOutcome.ResolutionConflict;
        }

        dbContext.ModerationAuditLogs.Add(new ModerationAuditLog
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
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ModerationActionOutcome.Success;
    }

    public async Task<ModerationActionOutcome> ApproveListingAsync(
        Guid listingId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Where(candidate => candidate.Id == listingId)
            .Select(candidate => new { candidate.Version, candidate.Status })
            .SingleOrDefaultAsync(cancellationToken);
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
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var listingUpdated = await dbContext.Listings
            .Where(candidate => candidate.Id == listingId && candidate.Version == listing.Version)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.Status, ListingStatus.Published)
                    .SetProperty(candidate => candidate.UpdatedAt, now)
                    .SetProperty(candidate => candidate.Version, candidate => candidate.Version + 1),
                cancellationToken);
        if (listingUpdated == 0)
        {
            return ModerationActionOutcome.ResolutionConflict;
        }

        dbContext.ModerationAuditLogs.Add(new ModerationAuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorId,
            Action = ModerationAuditAction.Approve,
            ListingId = listingId,
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ModerationActionOutcome.Success;
    }

    private IQueryable<ListingReport> ActiveReports(Guid listingId)
        => dbContext.ListingReports.Where(report => report.ListingId == listingId
            && (report.Status == ListingReportStatus.Open
                || report.Status == ListingReportStatus.InReview));

    private Task<Guid?> FindActiveListingIdAsync(Guid reportId, CancellationToken cancellationToken)
        => dbContext.ListingReports
            .AsNoTracking()
            .Where(report => report.Id == reportId
                && (report.Status == ListingReportStatus.Open
                    || report.Status == ListingReportStatus.InReview))
            .Select(report => (Guid?)report.ListingId)
            .FirstOrDefaultAsync(cancellationToken);

    private Task<Guid?> FindAnyListingIdAsync(Guid reportId, CancellationToken cancellationToken)
        => dbContext.ListingReports
            .AsNoTracking()
            .Where(report => report.Id == reportId)
            .Select(report => (Guid?)report.ListingId)
            .FirstOrDefaultAsync(cancellationToken);
}
