using RoomToRentRadar.Ports.Models.Listings;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Ports.Inbound;

public interface IListingsUseCase
{
    Task<ListingSearchResultDto> GetPageAsync(
        ListingQuery query,
        ListingCursor? cursor,
        CancellationToken cancellationToken);

    Task<ListingDetailResult> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ListingContactResult> GetContactAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ListingSummaryDto>?> GetSimilarAsync(
        Guid id,
        int limit,
        CancellationToken cancellationToken);
}
