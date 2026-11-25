using System.Threading;
using System.Threading.Tasks;
using Refit;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public interface ISearchFilterApiService
{
    [Get("/api/v1/listings/search")]
    Task<SearchListingsResponseContract> SearchListingsAsync(
        [Query] string campusId = "ictu-main",
        [Query] decimal? minRent = null,
        [Query] decimal? maxRent = null,
        [Query] double? minArea = null,
        [Query] double? maxArea = null,
        [Query] string? roomType = null,
        [Query] double? maxDistanceMeters = null,
        [Query] string sortBy = "distance",
        [Query] int pageIndex = 1,
        [Query] int pageSize = 20,
        CancellationToken cancellationToken = default);
}
