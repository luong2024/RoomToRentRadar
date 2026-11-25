using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

/// <summary>
/// Dịch vụ lọc phòng trọ offline khớp 1-1 với InMemoryListingRepository.SearchAsync (T04.1).
/// Không dùng room-2026-004 làm phòng trống vì room-2026-004 dành riêng cho test trạng thái Rented (T02.1).
/// </summary>
public sealed class MockSearchFilterService : ISearchFilterService
{
    private readonly IReadOnlyList<ListingCardDto> _catalog = BuildCatalog();

    public Task<SearchFilterFetchResult> SearchAsync(
        SearchListingsQueryContract query,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<ListingCardDto> filtered = _catalog
            .Where(l =>
                string.Equals(l.CampusId, query.CampusId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(l.Status, "Available", StringComparison.OrdinalIgnoreCase)
                && l.IsVerified);

        if (query.MinRent is { } minRent)
        {
            filtered = filtered.Where(l => l.Rent >= minRent);
        }

        if (query.MaxRent is { } maxRent)
        {
            filtered = filtered.Where(l => l.Rent <= maxRent);
        }

        if (query.MinArea is { } minArea)
        {
            filtered = filtered.Where(l => l.Area >= minArea);
        }

        if (query.MaxArea is { } maxArea)
        {
            filtered = filtered.Where(l => l.Area <= maxArea);
        }

        if (!string.IsNullOrWhiteSpace(query.RoomType))
        {
            filtered = filtered.Where(l => MatchesRoomType(l.RoomType, query.RoomType));
        }

        if (query.MaxDistanceMeters is { } maxDistance)
        {
            filtered = filtered.Where(l => l.DistanceMeters <= maxDistance);
        }

        filtered = (query.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "price" or "rent" or "price_asc" => filtered.OrderBy(l => l.Rent).ThenBy(l => l.DistanceMeters),
            "price_desc" or "rent_desc" => filtered.OrderByDescending(l => l.Rent).ThenBy(l => l.DistanceMeters),
            "area" or "area_desc" => filtered.OrderByDescending(l => l.Area).ThenBy(l => l.DistanceMeters),
            _ => filtered.OrderBy(l => l.DistanceMeters).ThenBy(l => l.Rent)
        };

        var allMatching = filtered.ToList();
        var pageIndex = Math.Max(1, query.PageIndex);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);
        var pagedItems = allMatching
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var response = new SearchListingsResponseContract
        {
            TotalCount = allMatching.Count,
            PageIndex = pageIndex,
            PageSize = pageSize,
            HasNextPage = pageIndex * pageSize < allMatching.Count,
            ActiveFilters = new SearchActiveFiltersContract
            {
                CampusId = query.CampusId,
                MinRent = query.MinRent,
                MaxRent = query.MaxRent,
                MinArea = query.MinArea,
                MaxArea = query.MaxArea,
                RoomType = query.RoomType,
                MaxDistanceMeters = query.MaxDistanceMeters,
                SortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "distance" : query.SortBy
            },
            Items = pagedItems
        };

        return Task.FromResult(new SearchFilterFetchResult(response, IsFromLiveApi: false));
    }

    private static bool MatchesRoomType(string itemRoomType, string queryRoomType)
    {
        if (IsClosedTypeAlias(itemRoomType) && IsClosedTypeAlias(queryRoomType))
        {
            return true;
        }

        return string.Equals(itemRoomType, queryRoomType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsClosedTypeAlias(string roomType) =>
        string.Equals(roomType, "Phòng khép kín", StringComparison.OrdinalIgnoreCase)
        || string.Equals(roomType, "Khép kín", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<ListingCardDto> BuildCatalog() =>
    [
        new ListingCardDto
        {
            Id = "room-2026-001",
            CampusId = "ictu-main",
            Title = "Studio Ban Công Thoáng Cách Cổng Chính ICTU 400m",
            Rent = 1_800_000m,
            Area = 22.5,
            RoomType = "Studio",
            DistanceMeters = 400,
            Address = "Ngõ 18 Đường Z115, Tân Thịnh, TP. Thái Nguyên",
            ThumbnailUrl = "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=700&q=80",
            Status = "Available",
            IsVerified = true
        },
        new ListingCardDto
        {
            Id = "room-2026-002",
            CampusId = "ictu-main",
            Title = "Studio Full Nội Thất Cổng Phụ ICTU",
            Rent = 1_950_000m,
            Area = 24.0,
            RoomType = "Studio",
            DistanceMeters = 350,
            Address = "Ngõ 42 Đường Z115, Tân Thịnh, TP. Thái Nguyên",
            ThumbnailUrl = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?auto=format&fit=crop&w=700&q=80",
            Status = "Available",
            IsVerified = true
        },
        new ListingCardDto
        {
            Id = "room-2026-003",
            CampusId = "ictu-main",
            Title = "Phòng Trọ Khép Kín Gác Xép Gỗ Thoáng Mát",
            Rent = 1_500_000m,
            Area = 20.0,
            RoomType = "Gác xép",
            DistanceMeters = 550,
            Address = "Đường Quang Trung, Tân Thịnh, TP. Thái Nguyên",
            ThumbnailUrl = "https://images.unsplash.com/photo-1598928506311-c55ded91a20c?auto=format&fit=crop&w=700&q=80",
            Status = "Available",
            IsVerified = true
        },
        new ListingCardDto
        {
            Id = "room-2026-009",
            CampusId = "ictu-main",
            Title = "Phòng Khép Kín Sinh Viên Giá Rẻ Gần Chợ Z115",
            Rent = 1_200_000m,
            Area = 16.0,
            RoomType = "Phòng khép kín",
            DistanceMeters = 700,
            Address = "Tổ 5 Phường Tân Thịnh, TP. Thái Nguyên",
            ThumbnailUrl = "https://images.unsplash.com/photo-1555854877-bab0e564b8d5?auto=format&fit=crop&w=700&q=80",
            Status = "Available",
            IsVerified = true
        },
        new ListingCardDto
        {
            Id = "room-2026-005",
            CampusId = "ictu-main",
            Title = "Chung Cư Mini Có Thang Máy & Khóa Vân Tay",
            Rent = 2_600_000m,
            Area = 28.0,
            RoomType = "Chung cư mini",
            DistanceMeters = 850,
            Address = "Đường Z115, Quyết Thắng, TP. Thái Nguyên",
            ThumbnailUrl = "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=700&q=80",
            Status = "Available",
            IsVerified = true
        },
        new ListingCardDto
        {
            Id = "room-2026-006",
            CampusId = "ictu-main",
            Title = "Phòng Khép Kín Ban Công Rộng Gần Hồ Điều Hòa",
            Rent = 1_650_000m,
            Area = 21.0,
            RoomType = "Phòng khép kín",
            DistanceMeters = 1200,
            Address = "Tổ 7 Phường Tân Thịnh, TP. Thái Nguyên",
            ThumbnailUrl = "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=700&q=80",
            Status = "Available",
            IsVerified = true
        },
        new ListingCardDto
        {
            Id = "room-2026-007",
            CampusId = "ictu-main",
            Title = "Phòng Gác Xép Có Điều Hòa Cách ICTU 1.5km",
            Rent = 1_350_000m,
            Area = 18.5,
            RoomType = "Gác xép",
            DistanceMeters = 1500,
            Address = "Phường Quyết Thắng, TP. Thái Nguyên",
            ThumbnailUrl = "https://images.unsplash.com/photo-1616594039964-ae9021a400a0?auto=format&fit=crop&w=700&q=80",
            Status = "Available",
            IsVerified = true
        },
        new ListingCardDto
        {
            Id = "room-2026-008",
            CampusId = "ictu-main",
            Title = "Chung Cư Mini 2 Giường Cho Nhóm Sinh Viên Ở Ghép",
            Rent = 3_200_000m,
            Area = 35.0,
            RoomType = "Chung cư mini",
            DistanceMeters = 1800,
            Address = "Đường Lương Ngọc Quyến, TP. Thái Nguyên",
            ThumbnailUrl = "https://images.unsplash.com/photo-1595526114035-0d45ed16cfbf?auto=format&fit=crop&w=700&q=80",
            Status = "Available",
            IsVerified = true
        }
    ];
}
