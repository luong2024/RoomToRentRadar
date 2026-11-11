using RoomToRentRadar.Features.RoomListings.Models;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadar.Features.RoomListings.Services;

/// <summary>
/// Fetches paginated room listings via <see cref="IApiClient"/>, with a DEBUG-only fallback
/// to realistic ICTU campus room data when the backend API is unreachable.
/// </summary>
public sealed class RoomListingsService : IRoomListingsService
{
    private readonly IApiClient _apiClient;
    private readonly bool _enableDebugFallback;

    public RoomListingsService(IApiClient apiClient, bool? enableDebugFallback = null)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        _apiClient = apiClient;
#if DEBUG
        _enableDebugFallback = enableDebugFallback ?? true;
#else
        _enableDebugFallback = enableDebugFallback ?? false;
#endif
    }

    public async Task<PagedResponse<RoomListItemModel>> GetRoomListingsAsync(
        int page,
        int pageSize,
        RoomSortOption sortBy = RoomSortOption.Newest,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var sortQuery = GetSortQueryValue(sortBy);
        var searchTerm = search?.Trim();
        var endpoint = $"api/rooms?page={page}&pageSize={pageSize}&sortBy={sortQuery}";
        if (!string.IsNullOrEmpty(searchTerm))
        {
            endpoint += $"&search={Uri.EscapeDataString(searchTerm)}";
        }

        try
        {
            var response = await _apiClient.GetAsync<PagedResponse<RoomListItemModel>>(
                endpoint,
                cancellationToken);

            return response ?? new PagedResponse<RoomListItemModel>([], page, pageSize, 0);
        }
        catch (Exception ex) when (
            _enableDebugFallback &&
            !cancellationToken.IsCancellationRequested &&
            (ex is HttpRequestException or InvalidOperationException or TaskCanceledException))
        {
            return GetFallbackMockPage(page, pageSize, sortBy, searchTerm);
        }
    }

    public async Task<RoomListItemModel?> GetRoomByIdAsync(
        string roomId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        var trimmedId = roomId.Trim();
        var endpoint = $"api/rooms/{Uri.EscapeDataString(trimmedId)}";

        try
        {
            return await _apiClient.GetAsync<RoomListItemModel>(endpoint, cancellationToken);
        }
        catch (Exception ex) when (
            _enableDebugFallback &&
            !cancellationToken.IsCancellationRequested &&
            (ex is HttpRequestException or InvalidOperationException or TaskCanceledException))
        {
            return MockRooms.FirstOrDefault(r =>
                string.Equals(r.Id, trimmedId, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string GetSortQueryValue(RoomSortOption sortBy) =>
        sortBy switch
        {
            RoomSortOption.PriceAsc => "price_asc",
            RoomSortOption.PriceDesc => "price_desc",
            RoomSortOption.DistanceAsc => "distance_asc",
            _ => "newest"
        };

    private static PagedResponse<RoomListItemModel> GetFallbackMockPage(
        int page,
        int pageSize,
        RoomSortOption sortBy,
        string? searchTerm)
    {
        IEnumerable<RoomListItemModel> query = MockRooms;

        // Filter the full mock catalog before paging, mirroring server-side search semantics.
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(r => MatchesSearch(r, searchTerm));
        }

        query = sortBy switch
        {
            RoomSortOption.PriceAsc => query.OrderBy(r => r.Price).ThenBy(r => r.Id),
            RoomSortOption.PriceDesc => query.OrderByDescending(r => r.Price).ThenBy(r => r.Id),
            RoomSortOption.DistanceAsc => query.OrderBy(r => r.DistanceToCampusKm).ThenBy(r => r.Id),
            _ => query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
        };

        var ordered = query.ToList();
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResponse<RoomListItemModel>(
            Items: items,
            Page: page,
            PageSize: pageSize,
            TotalCount: ordered.Count);
    }

    private static bool MatchesSearch(RoomListItemModel room, string term)
    {
        if (room.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            room.Address.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return room.Amenities?.Any(a => a.Contains(term, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static readonly IReadOnlyList<RoomListItemModel> MockRooms =
    [
        new()
        {
            Id = "room-ictu-001",
            Title = "Phòng trọ khép kín mới xây sát cổng chính ĐH CNTT&TT (ICTU)",
            Price = 1_800_000m,
            AreaSqm = 22,
            Address = "Ngõ 12 Đường Z115, Xã Quyết Thắng, TP. Thái Nguyên",
            DistanceToCampusKm = 0.3,
            ThumbnailUrl = "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?w=600&q=80",
            IsVerified = true,
            CreatedAt = new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.FromHours(7)),
            Amenities = ["Điều hòa", "Nóng lạnh", "Giờ giấc tự do", "Khóa vân tay"]
        },
        new()
        {
            Id = "room-ictu-002",
            Title = "Studio ban công thoáng mát khu tập thể giảng viên Quyết Thắng",
            Price = 2_300_000m,
            AreaSqm = 28,
            Address = "Đường Z115, Xóm Gốc Mít, Quyết Thắng, TP. Thái Nguyên",
            DistanceToCampusKm = 0.6,
            ThumbnailUrl = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=600&q=80",
            IsVerified = true,
            CreatedAt = new DateTimeOffset(2026, 11, 9, 14, 30, 0, TimeSpan.FromHours(7)),
            Amenities = ["Full nội thất", "Ban công", "Máy giặt chung"]
        },
        new()
        {
            Id = "room-ictu-003",
            Title = "Phòng trọ sinh viên giá tiết kiệm, có gác xép rộng rãi",
            Price = 1_200_000m,
            AreaSqm = 18,
            Address = "Xóm Tân Đức, Xã Quyết Thắng, TP. Thái Nguyên",
            DistanceToCampusKm = 0.8,
            ThumbnailUrl = "https://images.unsplash.com/photo-1598928506311-c55ded91a20c?w=600&q=80",
            IsVerified = true,
            CreatedAt = new DateTimeOffset(2026, 11, 9, 9, 15, 0, TimeSpan.FromHours(7)),
            Amenities = ["Gác xép", "Nóng lạnh", "Chỗ để xe rộng"]
        },
        new()
        {
            Id = "room-ictu-004",
            Title = "Căn hộ mini 1 ngủ 1 khách gần ngã ba Điểm Hẹn",
            Price = 2_800_000m,
            AreaSqm = 32,
            Address = "Phường Tân Thịnh, TP. Thái Nguyên",
            DistanceToCampusKm = 1.2,
            ThumbnailUrl = "https://images.unsplash.com/photo-1493809842364-78817add7ffb?w=600&q=80",
            IsVerified = true,
            CreatedAt = new DateTimeOffset(2026, 11, 8, 18, 0, 0, TimeSpan.FromHours(7)),
            Amenities = ["Điều hòa", "Tủ lạnh", "Bếp tách riêng"]
        },
        new()
        {
            Id = "room-ictu-005",
            Title = "Phòng trọ an ninh có camera 24/7 sau ký túc xá ICTU",
            Price = 1_500_000m,
            AreaSqm = 20,
            Address = "Ngõ 45 Đường Z115, Quyết Thắng, TP. Thái Nguyên",
            DistanceToCampusKm = 0.4,
            ThumbnailUrl = "https://images.unsplash.com/photo-1554995207-c18c203602cb?w=600&q=80",
            IsVerified = true,
            CreatedAt = new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.FromHours(7)),
            Amenities = ["Camera 24/7", "Nóng lạnh", "Wifi tốc độ cao"]
        },
        new()
        {
            Id = "room-ictu-006",
            Title = "Phòng đôi rộng rãi ở ghép 2–3 bạn gần ĐH Nông Lâm & ICTU",
            Price = 2_000_000m,
            AreaSqm = 26,
            Address = "Tổ 5, Phường Tân Thịnh, TP. Thái Nguyên",
            DistanceToCampusKm = 1.5,
            ThumbnailUrl = "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?w=600&q=80",
            IsVerified = false,
            CreatedAt = new DateTimeOffset(2026, 11, 7, 16, 45, 0, TimeSpan.FromHours(7)),
            Amenities = ["2 giường", "Điều hòa", "Bàn học đôi"]
        },
        new()
        {
            Id = "room-ictu-007",
            Title = "Phòng khép kín tầng 2 thoáng sáng, điện nước giá dân",
            Price = 1_650_000m,
            AreaSqm = 21,
            Address = "Đường Z115, Xã Quyết Thắng, TP. Thái Nguyên",
            DistanceToCampusKm = 0.5,
            ThumbnailUrl = "https://images.unsplash.com/photo-1513694203232-719a280e022f?w=600&q=80",
            IsVerified = true,
            CreatedAt = new DateTimeOffset(2026, 11, 7, 8, 20, 0, TimeSpan.FromHours(7)),
            Amenities = ["Điện nước giá dân", "Nóng lạnh", "Cửa sổ lớn"]
        },
        new()
        {
            Id = "room-ictu-008",
            Title = "Phòng trọ mới sơn sửa sạch đẹp, cách cổng phụ ICTU 300m",
            Price = 1_400_000m,
            AreaSqm = 19,
            Address = "Xóm Hồng Thái, Quyết Thắng, TP. Thái Nguyên",
            DistanceToCampusKm = 0.3,
            ThumbnailUrl = "https://images.unsplash.com/photo-1540518614846-7ede433c4573?w=600&q=80",
            IsVerified = true,
            CreatedAt = new DateTimeOffset(2026, 11, 6, 15, 0, 0, TimeSpan.FromHours(7)),
            Amenities = ["Giường tủ", "Nóng lạnh", "Không chung chủ"]
        },
        new()
        {
            Id = "room-ictu-009",
            Title = "Studio cao cấp có thang máy gần hồ điều hòa Tân Thịnh",
            Price = 2_600_000m,
            AreaSqm = 30,
            Address = "Tổ 8, Phường Tân Thịnh, TP. Thái Nguyên",
            DistanceToCampusKm = 1.4,
            ThumbnailUrl = "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?w=600&q=80",
            IsVerified = true,
            CreatedAt = new DateTimeOffset(2026, 11, 5, 11, 30, 0, TimeSpan.FromHours(7)),
            Amenities = ["Thang máy", "Điều hòa", "Tủ lạnh", "Khóa từ"]
        },
        new()
        {
            Id = "room-ictu-010",
            Title = "Phòng trọ đơn yên tĩnh phù hợp sinh viên năm cuối làm đồ án",
            Price = 1_350_000m,
            AreaSqm = 17,
            Address = "Ngõ 88 Đường Z115, Quyết Thắng, TP. Thái Nguyên",
            DistanceToCampusKm = 0.7,
            ThumbnailUrl = "https://images.unsplash.com/photo-1595526114035-0d45ed16cfbf?w=600&q=80",
            IsVerified = true,
            CreatedAt = new DateTimeOffset(2026, 11, 4, 19, 10, 0, TimeSpan.FromHours(7)),
            Amenities = ["Khu yên tĩnh", "Bàn làm việc", "Nóng lạnh"]
        },
        new()
        {
            Id = "room-ictu-011",
            Title = "Phòng trọ có sân phơi riêng và kệ bếp nấu ăn thoải mái",
            Price = 1_750_000m,
            AreaSqm = 23,
            Address = "Đường Quang Trung kéo dài, Phường Thịnh Đán, TP. Thái Nguyên",
            DistanceToCampusKm = 1.9,
            ThumbnailUrl = "https://images.unsplash.com/photo-1536376072261-38c75010e6c9?w=600&q=80",
            IsVerified = false,
            CreatedAt = new DateTimeOffset(2026, 11, 3, 13, 0, 0, TimeSpan.FromHours(7)),
            Amenities = ["Bếp riêng", "Sân phơi", "Điều hòa"]
        },
        new()
        {
            Id = "room-ictu-012",
            Title = "Phòng khép kín đầy đủ điều hòa, giường tủ sát chợ sinh viên",
            Price = 1_900_000m,
            AreaSqm = 24,
            Address = "Chợ sinh viên Z115, Quyết Thắng, TP. Thái Nguyên",
            DistanceToCampusKm = 0.5,
            ThumbnailUrl = "https://images.unsplash.com/photo-1522771739844-6a9f6d5f14af?w=600&q=80",
            IsVerified = true,
            CreatedAt = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.FromHours(7)),
            Amenities = ["Điều hòa", "Nóng lạnh", "Gần chợ & bến buýt"]
        },
        new()
        {
            Id = "room-ictu-013",
            Title = "Phòng trọ giá rẻ cho tân sinh viên ICTU, miễn phí giữ xe",
            Price = 1_100_000m,
            AreaSqm = 16,
            Address = "Xóm Gốc Mít, Xã Quyết Thắng, TP. Thái Nguyên",
            DistanceToCampusKm = 0.9,
            ThumbnailUrl = "https://images.unsplash.com/photo-1586023492125-27b2c045efd7?w=600&q=80",
            IsVerified = true,
            CreatedAt = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.FromHours(7)),
            Amenities = ["Miễn phí gửi xe", "Wifi", "Vệ sinh khép kín"]
        },
        new()
        {
            Id = "room-ictu-014",
            Title = "Căn hộ dịch vụ 2 giường đơn gần ĐH Khoa Học & ICTU",
            Price = 2_450_000m,
            AreaSqm = 29,
            Address = "Phường Tân Thịnh, TP. Thái Nguyên",
            DistanceToCampusKm = 1.6,
            ThumbnailUrl = "https://images.unsplash.com/photo-1560185127-6ed189bf02f4?w=600&q=80",
            IsVerified = true,
            CreatedAt = new DateTimeOffset(2026, 10, 31, 17, 0, 0, TimeSpan.FromHours(7)),
            Amenities = ["Full nội thất", "Máy giặt", "Khóa vân tay"]
        }
    ];
}
