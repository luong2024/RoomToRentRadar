using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.Infrastructure.Api;

public interface IApiClient
{
    Task<PagedResult<ListingSummaryDto>> GetListingsAsync(int limit, CancellationToken cancellationToken = default);
}
