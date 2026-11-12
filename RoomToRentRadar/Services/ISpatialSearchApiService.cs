using System.Threading;
using System.Threading.Tasks;
using Refit;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public interface ISpatialSearchApiService
{
    [Post("/api/v1/listings/spatial-search")]
    Task<SpatialSearchResponse> SearchInPolygonAsync(
        [Body] SpatialSearchRequest request,
        CancellationToken cancellationToken = default);
}