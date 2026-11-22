using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IListingReportRepository
{
    Task<bool> HasActiveReportAsync(
        Guid listingId,
        Guid reporterId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DateTime>> GetRecentReportTimesAsync(
        Guid reporterId,
        DateTime windowStart,
        CancellationToken cancellationToken);

    /// <summary>Returns <c>false</c> when the active-report unique index rejects the row.</summary>
    Task<bool> TryAddAsync(ListingReport report, CancellationToken cancellationToken);
}
