using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public interface ISpatialSearchService
{
    Task<SpatialSearchResponse> SearchInPolygonAsync(
        IReadOnlyList<double[]> closedGeoJsonRing,
        string sortBy = "distance",
        CancellationToken cancellationToken = default);
}