using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

/// <summary>
/// Sinh sẵn 45 phòng trọ mẫu quanh ĐH CNTT&amp;TT Thái Nguyên (ICTU - Đường Z115, Quyết Thắng).
/// Tất cả phòng đều Available và IsVerified = true theo US01 AC4.
/// </summary>
public sealed class MockListingsService : IListingsService
{
    private static readonly IReadOnlyList<ListingCardDto> AllMockRooms = GenerateRooms(45);

    public async Task<PagedResult<RoomListingItemModel>> GetListingsAsync(
        string campusId,
        int pageIndex,
        int pageSize,
        string sortBy,
        CancellationToken cancellationToken = default)
    {
        // Giả lập độ trễ mạng nhẹ để kiểm tra hiệu ứng tải trang & Infinite Scroll
        await Task.Delay(250, cancellationToken);

        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 50);

        // US01 AC4: Chỉ trả về phòng Available và đã xác minh (IsVerified = true)
        IEnumerable<ListingCardDto> query = AllMockRooms
            .Where(r => r.CampusId == campusId
                        && string.Equals(r.Status, "Available", StringComparison.OrdinalIgnoreCase)
                        && r.IsVerified);

        // US01 AC1: Mặc định sắp xếp khoảng cách gần cơ sở ICTU nhất
        query = sortBy switch
        {
            "price_asc" => query.OrderBy(r => r.Rent).ThenBy(r => r.DistanceMeters),
            "price_desc" => query.OrderByDescending(r => r.Rent).ThenBy(r => r.DistanceMeters),
            "area_desc" => query.OrderByDescending(r => r.Area).ThenBy(r => r.DistanceMeters),
            _ => query.OrderBy(r => r.DistanceMeters)
        };

        var sortedList = query.ToList();
        var totalCount = sortedList.Count;
        var pageItems = sortedList
            .Skip((safePageIndex - 1) * safePageSize)
            .Take(safePageSize)
            .Select(RoomListingItemModel.FromDto)
            .ToList();

        var hasNextPage = safePageIndex * safePageSize < totalCount;

        return new PagedResult<RoomListingItemModel>
        {
            Items = pageItems,
            PageIndex = safePageIndex,
            PageSize = safePageSize,
            TotalCount = totalCount,
            HasNextPage = hasNextPage
        };
    }

    private static List<ListingCardDto> GenerateRooms(int count)
    {
        string[] streets =
        [
            "Ngõ 112 Đường Z115, Quyết Thắng",
            "Ngõ 68 Đường Z115, Tân Thịnh",
            "Khu tập thể ĐH CNTT&TT, Quyết Thắng",
            "Đường Bắc Nam, P. Tân Thịnh",
            "Ngõ 198 Lương Ngọc Quyến, Quang Trung",
            "Xóm Tân Lập, Xã Quyết Thắng"
        ];

        string[] roomTypes = ["Studio", "Khép kín", "Chung chủ", "Mini Apartment"];
        decimal[] baseRents = [1_200_000m, 1_500_000m, 1_800_000m, 2_100_000m, 2_500_000m, 2_900_000m];

        var list = new List<ListingCardDto>(count);
        for (int i = 1; i <= count; i++)
        {
            var street = streets[(i - 1) % streets.Length];
            var roomType = roomTypes[(i - 1) % roomTypes.Length];
            var rent = baseRents[(i - 1) % baseRents.Length] + ((i % 3) * 100_000m);
            var area = 16.0 + ((i * 2.5) % 18.0);
            var distanceMeters = 120 + (i * 65); // từ 185m đến ~3.04km quanh ICTU

            list.Add(new ListingCardDto
            {
                Id = $"room-ictu-{i:000}",
                CampusId = "ictu-main",
                Title = $"Phòng {roomType} #{i:00} gần ICTU - {street.Split(',')[0]}",
                Rent = rent,
                Area = Math.Round(area, 1),
                DistanceMeters = distanceMeters,
                RoomType = roomType,
                Address = $"{street}, TP. Thái Nguyên",
                ThumbnailUrl = $"room_sample_{((i - 1) % 4) + 1}.png",
                Status = "Available",
                IsVerified = true
            });
        }

        return list;
    }
}