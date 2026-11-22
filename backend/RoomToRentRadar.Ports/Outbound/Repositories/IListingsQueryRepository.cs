using RoomToRentRadar.Ports.Models.Listings;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IListingsQueryRepository
{
    Task<int> CountPublishedAsync(
        ListingFilters filters,
        int cap,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ListingSummaryDto>> GetPublishedPageAsync(
        ListingFilters filters,
        ListingSort sort,
        ListingCursor? cursor,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ListingCandidate>> GetRadiusCandidatesAsync(
        ListingFilters filters,
        GeoBoundingBox box,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ListingCandidate>> GetAreaCandidatesAsync(
        ListingFilters filters,
        GeoBoundingBox bounds,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> GetCoverPhotoUrlsAsync(
        IReadOnlyCollection<Guid> listingIds,
        CancellationToken cancellationToken);

    Task<ListingDetailSnapshot?> GetDetailAsync(
        Guid listingId,
        CancellationToken cancellationToken);

    Task<ListingContactSnapshot?> GetContactAsync(
        Guid listingId,
        CancellationToken cancellationToken);

    Task<ListingSimilarSubject?> GetSimilarSubjectAsync(
        Guid listingId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ListingSummaryDto>> GetSimilarAsync(
        ListingSimilarSubject subject,
        long minPrice,
        long maxPrice,
        double minAreaM2,
        double maxAreaM2,
        int limit,
        CancellationToken cancellationToken);
}
