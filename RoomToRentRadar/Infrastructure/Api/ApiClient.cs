using System.Net.Http.Json;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.Infrastructure.Api;

public sealed class ApiClient(HttpClient httpClient) : IApiClient
{
    public async Task<PagedResult<ListingSummaryDto>> GetListingsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<PagedResult<ListingSummaryDto>>(
            $"api/v1/listings?limit={limit}",
            cancellationToken);

        return result ?? throw new InvalidOperationException("The listings endpoint returned an empty response.");
    }
}
