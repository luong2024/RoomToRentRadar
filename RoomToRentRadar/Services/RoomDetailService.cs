using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Refit;

namespace RoomToRentRadar.Services;

/// <summary>
/// Gọi Backend API T02.1 qua Refit; tự động fallback sang MockRoomDetailService
/// khi Backend local chưa bật hoặc mất kết nối.
/// </summary>
public sealed class RoomDetailService : IRoomDetailService
{
    private readonly IRoomDetailApiService _apiService;
    private readonly MockRoomDetailService _mockService;
    private readonly ILogger<RoomDetailService> _logger;

    public RoomDetailService(
        IRoomDetailApiService apiService,
        MockRoomDetailService mockService,
        ILogger<RoomDetailService> logger)
    {
        _apiService = apiService;
        _mockService = mockService;
        _logger = logger;
    }

    public async Task<RoomDetailFetchResult> GetRoomDetailAsync(
        string roomId,
        bool isAuthenticated = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return new RoomDetailFetchResult(null, IsFromLiveApi: false);
        }

        try
        {
            var detail = await _apiService.GetListingDetailAsync(roomId, cancellationToken);
            return new RoomDetailFetchResult(detail, IsFromLiveApi: true);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return new RoomDetailFetchResult(null, IsFromLiveApi: true);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                                 && ex is HttpRequestException or TaskCanceledException or ApiException)
        {
            _logger.LogWarning(
                ex,
                "Không thể kết nối Backend API T02.1 cho phòng {RoomId}. Chuyển sang dữ liệu mẫu offline.",
                roomId);

            return await _mockService.GetRoomDetailAsync(roomId, isAuthenticated, cancellationToken);
        }
    }
}
