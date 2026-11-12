using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Refit;
using RoomToRentRadar.Models;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

/// <summary>
/// Gọi Backend API qua Refit; tự động fallback sang MockListingsService
/// khi Backend local (http://10.0.2.2:5000) chưa bật hoặc không kết nối được.
/// </summary>
public sealed class ListingsService : IListingsService
{
    private readonly IListingsApiService _apiService;
    private readonly MockListingsService _mockService;
    private readonly ILogger<ListingsService> _logger;

    public ListingsService(
        IListingsApiService apiService,
        MockListingsService mockService,
        ILogger<ListingsService> logger)
    {
        _apiService = apiService;
        _mockService = mockService;
        _logger = logger;
    }

    public async Task<PagedResult<RoomListingItemModel>> GetListingsAsync(
        string campusId,
        int pageIndex,
        int pageSize,
        string sortBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var apiResult = await _apiService.GetListingsAsync(
                campusId, pageIndex, pageSize, sortBy, cancellationToken);

            // US01 AC4: Phòng vệ thêm ở client chỉ giữ phòng Available & IsVerified
            var mappedItems = apiResult.Items
                .Where(dto => string.Equals(dto.Status, "Available", StringComparison.OrdinalIgnoreCase)
                              && dto.IsVerified)
                .Select(RoomListingItemModel.FromDto)
                .ToList();

            return new PagedResult<RoomListingItemModel>
            {
                Items = mappedItems,
                PageIndex = apiResult.PageIndex,
                PageSize = apiResult.PageSize,
                TotalCount = apiResult.TotalCount,
                HasNextPage = apiResult.HasNextPage,
                ContinuationToken = apiResult.ContinuationToken
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                                 && ex is HttpRequestException or TaskCanceledException or ApiException)
        {
            _logger.LogWarning(ex,
                "Không thể kết nối Backend API tại trang {PageIndex}. Chuyển sang dữ liệu mẫu quanh ICTU.",
                pageIndex);

            return await _mockService.GetListingsAsync(
                campusId, pageIndex, pageSize, sortBy, cancellationToken);
        }
    }
}