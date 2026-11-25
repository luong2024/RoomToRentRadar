using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Refit;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

/// <summary>
/// Gọi Backend API PUT /api/v1/listings/{id}/house-rules (T11.1);
/// tự động fallback sang MockHouseRulesService nếu API chưa bật.
/// </summary>
public sealed class HouseRulesService : IHouseRulesService
{
    private readonly IHouseRulesApiService _apiService;
    private readonly MockHouseRulesService _mockService;
    private readonly ILogger<HouseRulesService> _logger;

    public HouseRulesService(
        IHouseRulesApiService apiService,
        MockHouseRulesService mockService,
        ILogger<HouseRulesService> logger)
    {
        _apiService = apiService;
        _mockService = mockService;
        _logger = logger;
    }

    public async Task<HouseRulesUpdateResult> UpdateHouseRulesAsync(
        string listingId,
        UpdateHouseRulesRequestContract request,
        HouseRulesResponseContract? currentState = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiService.UpdateHouseRulesAsync(listingId, request, cancellationToken);
            return new HouseRulesUpdateResult(response, IsFromLiveApi: true);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                                 && ex is HttpRequestException or TaskCanceledException or ApiException)
        {
            _logger.LogWarning(
                ex,
                "Không thể kết nối Backend API T11.1 cho phòng {ListingId}. Chuyển sang MockHouseRulesService.",
                listingId);

            return await _mockService.UpdateHouseRulesAsync(listingId, request, currentState, cancellationToken);
        }
    }
}
