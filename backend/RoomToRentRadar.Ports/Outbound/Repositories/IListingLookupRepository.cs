using RoomToRentRadar.Ports.Models.Listings;
using RoomToRentRadar.Ports.Models.Reports;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IListingLookupRepository
{
    Task<bool> ExistsAsync(Guid listingId, CancellationToken cancellationToken);

    /// <summary>The published listing's report target, or <c>null</c> when it is not visible.</summary>
    Task<ListingReportTarget?> FindPublishedReportTargetAsync(
        Guid listingId,
        CancellationToken cancellationToken);

    Task<ListingLocation?> FindPublishedLocationAsync(
        Guid listingId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PublishedListing>> GetPublishedUpdatedSinceAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken);
}
