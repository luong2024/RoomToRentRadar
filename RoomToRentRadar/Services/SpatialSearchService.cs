using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public sealed class SpatialSearchService : ISpatialSearchService
{
    private readonly ISpatialSearchApiService _apiService;
    private readonly MockSpatialSearchService _mockService;
    private readonly ILogger<SpatialSearchService> _logger;

    public SpatialSearchService(
        ISpatialSearchApiService apiService,
        MockSpatialSearchService mockService,
        ILogger<SpatialSearchService> logger)
    {
        _apiService = apiService;
        _mockService = mockService;
        _logger = logger;
    }

    public async Task<SpatialSearchResponse> SearchInPolygonAsync(
        IReadOnlyList<double[]> closedGeoJsonRing,
        string sortBy = "distance",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new SpatialSearchRequest
            {
                Polygon = [.. closedGeoJsonRing],
                SortBy = sortBy
            };
            return await _apiService.SearchInPolygonAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Backend T05.2 chưa khả dụng, chuyển sang MockSpatialSearchService.");
            return await _mockService.SearchInPolygonAsync(closedGeoJsonRing, sortBy, cancellationToken);
        }
    }
}