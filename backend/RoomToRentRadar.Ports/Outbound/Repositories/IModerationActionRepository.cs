using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Models.Moderation;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IModerationActionRepository
{
    Task<Guid?> FindActiveReportListingIdAsync(
        Guid reportId,
        CancellationToken cancellationToken);

    Task<Guid?> FindReportListingIdAsync(
        Guid reportId,
        CancellationToken cancellationToken);

    Task<bool> HasActiveClaimByOtherAsync(
        Guid listingId,
        Guid actorId,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<int> CountActiveReportsAsync(Guid listingId, CancellationToken cancellationToken);

    Task<int> ClaimActiveReportsAsync(
        Guid listingId,
        Guid actorId,
        DateTime utcNow,
        DateTime claimedUntilUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetActiveClaimOwnersAsync(
        Guid listingId,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<int> ReleaseActiveReportsAsync(
        Guid listingId,
        Guid actorId,
        bool isAdmin,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ListingReportStatus>> GetActiveReportStatusesAsync(
        Guid listingId,
        CancellationToken cancellationToken);

    Task<ModerationListingState?> GetListingStateAsync(
        Guid listingId,
        CancellationToken cancellationToken);

    Task<int> TransitionListingAsync(
        Guid listingId,
        long expectedVersion,
        ListingStatus nextStatus,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<int> ResolveActiveReportsAsync(
        Guid listingId,
        ListingReportStatus nextStatus,
        CancellationToken cancellationToken);

    void AddAuditLog(ModerationAuditLog log);
}
