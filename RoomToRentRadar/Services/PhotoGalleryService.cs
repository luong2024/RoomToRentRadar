using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Devices;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services;

public sealed class PhotoGalleryService : IPhotoGalleryService
{
    private readonly IPhotoGalleryApiService _apiService;
    private readonly MockPhotoGalleryService _mockService;
    private readonly ILogger<PhotoGalleryService> _logger;

    public PhotoGalleryService(
        IPhotoGalleryApiService apiService,
        MockPhotoGalleryService mockService,
        ILogger<PhotoGalleryService> logger)
    {
        _apiService = apiService;
        _mockService = mockService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PhotoGalleryItemModel>> GetPhotosAsync(
        string listingId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var baseUrl = DeviceInfo.Platform == DevicePlatform.Android
                ? "http://10.0.2.2:5000"
                : "http://localhost:5000";

            var response = await _apiService.GetGalleryAsync(listingId, cancellationToken);
            var photos = response.Photos ?? [];

            return photos
                .OrderBy(p => p.Order)
                .Select(p => PhotoGalleryItemModel.FromDto(p, baseUrl))
                .ToList();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Backend T03.1 chưa khả dụng cho phòng {ListingId}, dùng dữ liệu ảnh mẫu.", listingId);
            return await _mockService.GetPhotosAsync(listingId, cancellationToken);
        }
    }
}
