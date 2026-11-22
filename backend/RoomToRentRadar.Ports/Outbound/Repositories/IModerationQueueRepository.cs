using RoomToRentRadar.Ports.Models.Moderation;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Moderation;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IModerationQueueRepository
{
    Task<IReadOnlyList<ModerationGroupRow>> LoadGroupPageAsync(
        int take,
        ModerationQueueCursor? cursor,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ModerationListingSummary>> GetListingsAsync(
        IReadOnlyCollection<Guid> listingIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ModerationReasonCountRow>> GetActiveReasonCountsAsync(
        IReadOnlyCollection<Guid> listingIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ModerationActiveClaim>> GetActiveClaimsAsync(
        IReadOnlyCollection<Guid> listingIds,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetActiveReportIdsAsync(
        Guid listingId,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ModerationListingDto>> GetListingQueueAsync(
        ListingStatus status,
        ModerationListingQueueCursor? cursor,
        int take,
        CancellationToken cancellationToken);

    Task<ModerationReportDetailDto?> GetReportAsync(
        Guid reportId,
        CancellationToken cancellationToken);
}
