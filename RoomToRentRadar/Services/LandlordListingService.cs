using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Refit;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

/// <summary>
/// Gửi yêu cầu đăng tin phòng trọ lên Backend T08.2 qua Refit;
/// tự động fallback sang MockLandlordListingService nếu API T08.2 chưa bật.
/// </summary>
public sealed class LandlordListingService : ILandlordListingService
{
    private readonly ILandlordListingApiService _apiService;
    private readonly MockLandlordListingService _mockService;
    private readonly ILogger<LandlordListingService> _logger;

    public LandlordListingService(
        ILandlordListingApiService apiService,
        MockLandlordListingService mockService,
        ILogger<LandlordListingService> logger)
    {
        _apiService = apiService;
        _mockService = mockService;
        _logger = logger;
    }

    public async Task<CreateListingSubmitResult> SubmitListingAsync(
        CreateListingRequestContract request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiService.CreateListingAsync(request, cancellationToken);
            return new CreateListingSubmitResult(response, IsFromLiveApi: true);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                                 && ex is HttpRequestException or TaskCanceledException or ApiException)
        {
            _logger.LogWarning(
                ex,
                "Không thể kết nối Backend API T08.2 để đăng tin. Chuyển sang MockLandlordListingService (PendingReview).");

            return await _mockService.SubmitListingAsync(request, cancellationToken);
        }
    }
}
