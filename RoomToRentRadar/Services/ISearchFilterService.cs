using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public sealed record SearchFilterFetchResult(SearchListingsResponseContract Response, bool IsFromLiveApi);

public interface ISearchFilterService
{
    Task<SearchFilterFetchResult> SearchAsync(
        SearchListingsQueryContract query,
        CancellationToken cancellationToken = default);
}
