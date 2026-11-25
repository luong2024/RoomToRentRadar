using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Refit;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

/// <summary>
/// Gọi Backend API T04.1 qua Refit; tự động fallback sang MockSearchFilterService
/// khi Backend local chưa bật hoặc mất kết nối.
/// </summary>
public sealed class SearchFilterService : ISearchFilterService
{
    private readonly ISearchFilterApiService _apiService;
    private readonly MockSearchFilterService _mockService;
    private readonly ILogger<SearchFilterService> _logger;

    public SearchFilterService(
        ISearchFilterApiService apiService,
        MockSearchFilterService mockService,
        ILogger<SearchFilterService> logger)
    {
        _apiService = apiService;
        _mockService = mockService;
        _logger = logger;
    }

    public async Task<SearchFilterFetchResult> SearchAsync(
        SearchListingsQueryContract query,
        CancellationToken cancellationToken = default)
    {
        var mappedRoomType = string.Equals(query.RoomType, "Phòng khép kín", StringComparison.OrdinalIgnoreCase)
            ? "Khép kín"
            : string.Equals(query.RoomType, "Tất cả", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(query.RoomType)
                ? null
                : query.RoomType;

        try
        {
            var response = await _apiService.SearchListingsAsync(
                campusId: query.CampusId,
                minRent: query.MinRent,
                maxRent: query.MaxRent,
                minArea: query.MinArea,
                maxArea: query.MaxArea,
                roomType: mappedRoomType,
                maxDistanceMeters: query.MaxDistanceMeters,
                sortBy: string.IsNullOrWhiteSpace(query.SortBy) ? "distance" : query.SortBy,
                pageIndex: query.PageIndex,
                pageSize: query.PageSize,
                cancellationToken: cancellationToken);

            return new SearchFilterFetchResult(response, IsFromLiveApi: true);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                                 && ex is HttpRequestException or TaskCanceledException or ApiException)
        {
            _logger.LogWarning(
                ex,
                "Không thể kết nối Backend API T04.1 khi lọc phòng. Chuyển sang bộ lọc offline.");

            return await _mockService.SearchAsync(query, cancellationToken);
        }
    }
}
