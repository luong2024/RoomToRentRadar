using System.Net.Http.Json;
using RoomToRentRadar.Features.RoomDetails.Models;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadar.Features.RoomDetails.Services;

/// <summary>
/// Fetches real room details via <see cref="IApiClient"/> from the Backend API,
/// with a DEBUG fallback to ICTU campus rooms when offline.
/// </summary>
public class RoomDetailService : IRoomDetailService
{
    private readonly IApiClient _apiClient;
    private readonly bool _enableDebugFallback;

    public RoomDetailService(IApiClient apiClient, bool? enableDebugFallback = null)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
#if DEBUG
        _enableDebugFallback = enableDebugFallback ?? true;
#else
        _enableDebugFallback = enableDebugFallback ?? false;
#endif
    }

    public async Task<RoomDetailModel?> GetRoomDetailAsync(string roomId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return null;
        }

        var trimmedId = roomId.Trim();
        var endpoint = $"api/rooms/{Uri.EscapeDataString(trimmedId)}";

        try
        {
            var dto = await _apiClient.GetAsync<RoomDetailApiDto>(endpoint, cancellationToken);
            if (dto is not null)
            {
                return MapDtoToModel(dto);
            }

            return null;
        }
        catch (Exception ex) when (
            _enableDebugFallback &&
            !cancellationToken.IsCancellationRequested &&
            (ex is HttpRequestException or InvalidOperationException or TaskCanceledException))
        {
            return GetFallbackMockRoom(trimmedId);
        }
    }

    public async Task<List<SimilarRoomModel>> GetSimilarRoomsAsync(string roomId, CancellationToken cancellationToken = default)
    {
        var detail = await GetRoomDetailAsync(roomId, cancellationToken);
        if (detail?.SimilarRooms is { Count: > 0 })
        {
            return detail.SimilarRooms;
        }

        return GetFallbackSimilarRooms(roomId);
    }

    private static RoomDetailModel MapDtoToModel(RoomDetailApiDto dto)
    {
        var status = ParseStatus(dto.Status, dto.IsAvailable);

        var model = new RoomDetailModel
        {
            Id = dto.Id,
            Title = dto.Title,
            Description = dto.Description,
            Price = dto.Price,
            Deposit = dto.Deposit > 0 ? dto.Deposit : dto.Price / 2,
            AreaSquareMeters = dto.Area,
            RoomType = string.IsNullOrWhiteSpace(dto.RoomType) ? "Phòng trọ khép kín" : dto.RoomType,
            Address = dto.Address,
            Status = status,
            LastUpdated = dto.LastUpdatedDate != default ? dto.LastUpdatedDate : DateTime.Now,
            IsVerified = dto.IsVerified,
            IsAvailable = dto.IsAvailable,
            UnavailableMessage = dto.UnavailableMessage,
            LandlordPhone = dto.LandlordPhone,
            HouseRules = dto.HouseRules ?? [],
            ImageUrls = GetRoomImages(dto.Id),
            Amenities = (dto.Amenities ?? []).Select(a => new RoomAmenityModel
            {
                Name = a,
                IconGlyph = ResolveAmenityGlyph(a)
            }).ToList(),
            SimilarRooms = (dto.SimilarRooms ?? []).Select(s => new SimilarRoomModel
            {
                Id = s.Id,
                Title = s.Title,
                Price = s.Price,
                AreaSquareMeters = s.Area,
                Address = s.Address,
                RoomType = string.IsNullOrWhiteSpace(s.RoomType) ? "Phòng khép kín" : s.RoomType,
                ThumbnailUrl = GetRoomImages(s.Id).FirstOrDefault() ?? "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?w=600",
                IsVerified = s.IsVerified
            }).ToList()
        };

        return model;
    }

    private static ListingStatus ParseStatus(string? statusString, bool isAvailable)
    {
        if (!isAvailable)
        {
            if (string.Equals(statusString, "Rented", StringComparison.OrdinalIgnoreCase))
            {
                return ListingStatus.Rented;
            }
            return ListingStatus.Unavailable;
        }

        return ListingStatus.Available;
    }

    private static string ResolveAmenityGlyph(string amenity)
    {
        var lower = amenity.ToLowerInvariant();
        if (lower.Contains("wifi") || lower.Contains("mạng")) return "\uf8fe"; // wifi
        if (lower.Contains("điều hòa") || lower.Contains("máy lạnh")) return "\uf15f"; // snowflake
        if (lower.Contains("nóng lạnh") || lower.Contains("bình nóng")) return "\uf3e2"; // drop
        if (lower.Contains("máy giặt") || lower.Contains("giặt")) return "\uf893"; // washer
        if (lower.Contains("xe") || lower.Contains("bãi")) return "\uf8c4"; // car
        if (lower.Contains("an ninh") || lower.Contains("camera") || lower.Contains("bảo vệ")) return "\uf6a4"; // shield
        if (lower.Contains("tủ lạnh")) return "\uf63b";
        if (lower.Contains("giường") || lower.Contains("nệm")) return "\uf1ee";
        return "\uf2c8"; // checkmark
    }

    private static List<string> GetRoomImages(string roomId)
    {
        return
        [
            "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?w=1000",
            "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=1000",
            "https://images.unsplash.com/photo-1493809842364-78817add7ffb?w=1000"
        ];
    }

    private static RoomDetailModel GetFallbackMockRoom(string roomId)
    {
        var status = roomId.ToLowerInvariant() switch
        {
            "rented" or "room-02" => ListingStatus.Rented,
            "unavailable" or "room-03" => ListingStatus.Unavailable,
            _ => ListingStatus.Available
        };

        var isAvailable = status == ListingStatus.Available;

        return new RoomDetailModel
        {
            Id = roomId,
            Title = isAvailable
                ? "Phòng trọ cao cấp khép kín sát cổng ICTU"
                : "Phòng trọ sinh viên ban công thoáng mát",
            Description = "Phòng trọ mới xây sạch sẽ, đầy đủ nội thất cơ bản, an ninh đảm bảo 24/7, giờ giấc tự do không chung chủ. Vị trí thuận tiện cách cổng chính Đại học CNTT&TT (ICTU) chỉ 300m, gần chợ và các quán ăn sinh viên.",
            Price = 2000000m,
            Deposit = 1000000m,
            AreaSquareMeters = 25.0,
            RoomType = "Phòng trọ khép kín",
            Address = isAvailable ? "Số 15 Ngõ 12 Đường Z115, Xã Quyết Thắng, TP. Thái Nguyên" : string.Empty,
            Status = status,
            IsAvailable = isAvailable,
            UnavailableMessage = isAvailable ? null : "Phòng này hiện không khả dụng (đã có người thuê hoặc đang bảo trì).",
            LastUpdated = DateTime.Now.AddDays(-1),
            SittingRooms = 1,
            Bedrooms = 1,
            Bathrooms = 1,
            Kitchens = 1,
            Rating = 4.9,
            ReviewCount = 18,
            LandlordName = "Bác Hùng (Chủ trọ ICTU)",
            LandlordPhone = isAvailable ? "0982 345 678" : string.Empty,
            LandlordEmail = "chutro.ictu@gmail.com",
            LandlordAvatar = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=200",
            IsVerified = true,
            ImageUrls = GetRoomImages(roomId),
            Amenities =
            [
                new() { Name = "Wifi cáp quang tốc độ cao", IconGlyph = "\uf8fe" },
                new() { Name = "Điều hòa Inverter tiết kiệm điện", IconGlyph = "\uf15f" },
                new() { Name = "Bình nóng lạnh", IconGlyph = "\uf3e2" },
                new() { Name = "Khu máy giặt chung", IconGlyph = "\uf893" },
                new() { Name = "Nhà để xe rộng rãi có mái che", IconGlyph = "\uf8c4" },
                new() { Name = "Camera giám sát an ninh 24/7", IconGlyph = "\uf6a4" }
            ],
            HouseRules =
            [
                "Giữ trật tự chung sau 23:00 hàng ngày",
                "Không nuôi thú cưng (chó, mèo) trong phòng",
                "Khóa cổng chính cẩn thận khi ra vào sau 23h30",
                "Vệ sinh sạch sẽ khu vực hành lang và bếp chung"
            ],
            SimilarRooms = isAvailable ? [] : GetFallbackSimilarRooms(roomId)
        };
    }

    private static List<SimilarRoomModel> GetFallbackSimilarRooms(string excludeRoomId)
    {
        return
        [
            new()
            {
                Id = "sim-ictu-01",
                Title = "Phòng trọ ngõ 18 Đường Z115 (Còn trống)",
                Price = 1800000m,
                AreaSquareMeters = 22.0,
                Address = "Ngõ 18 Đường Z115, Xã Quyết Thắng, TP. Thái Nguyên",
                RoomType = "Phòng khép kín",
                ThumbnailUrl = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=500",
                IsVerified = true
            },
            new()
            {
                Id = "sim-ictu-02",
                Title = "Studio full đồ gần cổng phụ ĐH CNTT&TT",
                Price = 2200000m,
                AreaSquareMeters = 28.0,
                Address = "Khu đô thị Quyết Thắng, TP. Thái Nguyên",
                RoomType = "Studio cao cấp",
                ThumbnailUrl = "https://images.unsplash.com/photo-1493809842364-78817add7ffb?w=500",
                IsVerified = true
            }
        ];
    }
}
