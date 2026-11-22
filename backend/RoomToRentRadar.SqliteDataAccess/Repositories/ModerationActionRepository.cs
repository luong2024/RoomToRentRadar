using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Models.Moderation;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.SqliteDataAccess.Repositories;

public sealed class ModerationActionRepository(AppDbContext dbContext) : IModerationActionRepository
{
    public Task<Guid?> FindActiveReportListingIdAsync(
        Guid reportId,
        CancellationToken cancellationToken)
        => dbContext.ListingReports
            .AsNoTracking()
            .Where(report => report.Id == reportId
                && (report.Status == ListingReportStatus.Open
                    || report.Status == ListingReportStatus.InReview))
            .Select(report => (Guid?)report.ListingId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Guid?> FindReportListingIdAsync(
        Guid reportId,
        CancellationToken cancellationToken)
        => dbContext.ListingReports
            .AsNoTracking()
            .Where(report => report.Id == reportId)
            .Select(report => (Guid?)report.ListingId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasActiveClaimByOtherAsync(
        Guid listingId,
        Guid actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
        => ActiveReports(listingId)
            .AsNoTracking()
            .AnyAsync(
                report => report.ClaimedByUserId != null
                    && report.ClaimedByUserId != actorId
                    && report.ClaimedUntilUtc > utcNow,
                cancellationToken);

    public Task<int> CountActiveReportsAsync(Guid listingId, CancellationToken cancellationToken)
        => ActiveReports(listingId).CountAsync(cancellationToken);

    public Task<int> ClaimActiveReportsAsync(
        Guid listingId,
        Guid actorId,
        DateTime utcNow,
        DateTime claimedUntilUtc,
        CancellationToken cancellationToken)
        => ActiveReports(listingId)
            .Where(report => report.ClaimedByUserId == null
                || report.ClaimedByUserId == actorId
                || report.ClaimedUntilUtc == null
                || report.ClaimedUntilUtc <= utcNow)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(report => report.Status, ListingReportStatus.InReview)
                    .SetProperty(report => report.ClaimedByUserId, actorId)
                    .SetProperty(report => report.ClaimedUntilUtc, claimedUntilUtc)
                    .SetProperty(report => report.Version, report => report.Version + 1),
                cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetActiveClaimOwnersAsync(
        Guid listingId,
        DateTime utcNow,
        CancellationToken cancellationToken)
        => await ActiveReports(listingId)
            .AsNoTracking()
            .Where(report => report.ClaimedByUserId != null && report.ClaimedUntilUtc > utcNow)
            .Select(report => report.ClaimedByUserId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

    public Task<int> ReleaseActiveReportsAsync(
        Guid listingId,
        Guid actorId,
        bool isAdmin,
        DateTime utcNow,
        CancellationToken cancellationToken)
        => ActiveReports(listingId)
            .Where(report => report.ClaimedByUserId != null
                && report.ClaimedUntilUtc > utcNow
                && (isAdmin || report.ClaimedByUserId == actorId))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(report => report.ClaimedByUserId, (Guid?)null)
                    .SetProperty(report => report.ClaimedUntilUtc, (DateTime?)null)
                    .SetProperty(report => report.Version, report => report.Version + 1),
                cancellationToken);

    public async Task<IReadOnlyList<ListingReportStatus>> GetActiveReportStatusesAsync(
        Guid listingId,
        CancellationToken cancellationToken)
        => await ActiveReports(listingId)
            .AsNoTracking()
            .Select(report => report.Status)
            .Distinct()
            .ToListAsync(cancellationToken);

    public Task<ModerationListingState?> GetListingStateAsync(
        Guid listingId,
        CancellationToken cancellationToken)
        => dbContext.Listings
            .AsNoTracking()
            .Where(candidate => candidate.Id == listingId)
            .Select(candidate => new ModerationListingState(
                candidate.Version,
                candidate.Status))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<int> TransitionListingAsync(
        Guid listingId,
        long expectedVersion,
        ListingStatus nextStatus,
        DateTime utcNow,
        CancellationToken cancellationToken)
        => dbContext.Listings
            .Where(candidate => candidate.Id == listingId && candidate.Version == expectedVersion)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.Status, nextStatus)
                    .SetProperty(candidate => candidate.UpdatedAt, utcNow)
                    .SetProperty(candidate => candidate.Version, candidate => candidate.Version + 1),
                cancellationToken);

    public Task<int> ResolveActiveReportsAsync(
        Guid listingId,
        ListingReportStatus nextStatus,
        CancellationToken cancellationToken)
        => ActiveReports(listingId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(report => report.Status, nextStatus)
                    .SetProperty(report => report.ClaimedByUserId, (Guid?)null)
                    .SetProperty(report => report.ClaimedUntilUtc, (DateTime?)null)
                    .SetProperty(report => report.Version, report => report.Version + 1),
                cancellationToken);

    public void AddAuditLog(ModerationAuditLog log)
        => dbContext.ModerationAuditLogs.Add(log);

    private IQueryable<ListingReport> ActiveReports(Guid listingId)
        => dbContext.ListingReports.Where(report => report.ListingId == listingId
            && (report.Status == ListingReportStatus.Open
                || report.Status == ListingReportStatus.InReview));
}
