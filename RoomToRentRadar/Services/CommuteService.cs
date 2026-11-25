using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

/// <summary>
/// Gọi Backend API T09.1; khi API định tuyến chưa sẵn sàng hoặc lỗi thì fallback sang Mock / Haversine.
/// </summary>
public sealed class CommuteService : ICommuteService
{
    private readonly ICommuteApiService _apiService;
    private readonly MockCommuteService _mockService;
    private readonly ILogger<CommuteService> _logger;

    public CommuteService(
        ICommuteApiService apiService,
        MockCommuteService mockService,
        ILogger<CommuteService> logger)
    {
        _apiService = apiService;
        _mockService = mockService;
        _logger = logger;
    }

    public IReadOnlyList<CampusDestinationOption> GetCampusDestinations() => _mockService.GetCampusDestinations();

    public async Task<CommuteEstimateDto> GetCommuteEstimateAsync(
        string listingId,
        CampusDestinationOption campus,
        double? roomLat = null,
        double? roomLng = null,
        bool forceFallback = false,
        CancellationToken cancellationToken = default)
    {
        if (forceFallback)
        {
            return await _mockService.GetCommuteEstimateAsync(
                listingId,
                campus,
                roomLat,
                roomLng,
                forceFallback: true,
                cancellationToken);
        }

        try
        {
            return await _apiService.GetCommuteEstimateAsync(listingId, campus.CampusId, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex,
                "Backend T09.1 chưa khả dụng cho phòng {ListingId} -> {CampusId}. Chuyển sang MockCommuteService.",
                listingId,
                campus.CampusId);

            return await _mockService.GetCommuteEstimateAsync(
                listingId,
                campus,
                roomLat,
                roomLng,
                forceFallback: false,
                cancellationToken);
        }
    }
}
