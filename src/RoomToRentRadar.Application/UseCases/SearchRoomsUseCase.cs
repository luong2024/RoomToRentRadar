using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.UseCases;

public class SearchRoomsUseCase : ISearchRoomsUseCase
{
    public const int MaxPageSize = 50;
    public const int DefaultPageSize = 10;

    private readonly IRoomRepository _roomRepository;

    public SearchRoomsUseCase(IRoomRepository roomRepository)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
    }

    public async Task<SearchRoomsResult> ExecuteAsync(
        SearchRoomsQuery query,
        CancellationToken cancellationToken = default)
    {
        query ??= new SearchRoomsQuery();

        ValidateQuery(query);

        var (items, totalCount) = await _roomRepository.SearchAsync(query, cancellationToken);

        // AC4: Bắt buộc chỉ trả về các phòng có Status == Available và IsVerified == true
        var validRooms = items
            .Where(r => r.Status == RoomStatus.Available && r.IsVerified)
            .ToList();

        var dtos = validRooms.Select(r => new RoomSummaryDto(
            r.Id,
            r.Title,
            r.Price,
            r.Area,
            r.RoomType,
            r.IsVerified,
            CalculateHaversineDistanceKm(query.Latitude, query.Longitude, r.Latitude, r.Longitude)
        )).ToList();

        var pageSize = Math.Clamp(query.PageSize <= 0 ? DefaultPageSize : query.PageSize, 1, MaxPageSize);
        var currentPage = Math.Max(1, query.PageNumber);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);
        var activeFilters = DetermineActiveFilters(query);

        return new SearchRoomsResult(
            dtos,
            totalCount,
            totalPages,
            currentPage,
            activeFilters);
    }

    private static void ValidateQuery(SearchRoomsQuery query)
    {
        if (query.PageNumber < 1)
        {
            throw new ArgumentException("PageNumber phải lớn hơn hoặc bằng 1.");
        }

        if (query.PageSize < 1)
        {
            throw new ArgumentException("PageSize phải lớn hơn hoặc bằng 1.");
        }

        if (query.PageSize > MaxPageSize)
        {
            throw new ArgumentException($"PageSize không được vượt quá {MaxPageSize}.");
        }

        if (query.MinPrice.HasValue && query.MaxPrice.HasValue && query.MinPrice.Value > query.MaxPrice.Value)
        {
            throw new ArgumentException("MinPrice không được lớn hơn MaxPrice.");
        }

        if (query.MinPrice.HasValue && query.MinPrice.Value < 0)
        {
            throw new ArgumentException("MinPrice không được âm.");
        }

        if (query.MaxPrice.HasValue && query.MaxPrice.Value < 0)
        {
            throw new ArgumentException("MaxPrice không được âm.");
        }

        if (query.MinArea.HasValue && query.MaxArea.HasValue && query.MinArea.Value > query.MaxArea.Value)
        {
            throw new ArgumentException("MinArea không được lớn hơn MaxArea.");
        }

        if (query.MinArea.HasValue && query.MinArea.Value < 0)
        {
            throw new ArgumentException("MinArea không được âm.");
        }

        if (query.MaxArea.HasValue && query.MaxArea.Value < 0)
        {
            throw new ArgumentException("MaxArea không được âm.");
        }

        if (query.RadiusInKm.HasValue && query.RadiusInKm.Value <= 0)
        {
            throw new ArgumentException("RadiusInKm phải lớn hơn 0.");
        }

        if (query.RadiusInKm.HasValue && (!query.Latitude.HasValue || !query.Longitude.HasValue))
        {
            throw new ArgumentException("Tọa độ Latitude và Longitude là bắt buộc khi sử dụng lọc theo bán kính RadiusInKm.");
        }

        if (query.Latitude.HasValue && (query.Latitude.Value < -90 || query.Latitude.Value > 90))
        {
            throw new ArgumentException("Latitude không hợp lệ (-90 đến 90).");
        }

        if (query.Longitude.HasValue && (query.Longitude.Value < -180 || query.Longitude.Value > 180))
        {
            throw new ArgumentException("Longitude không hợp lệ (-180 đến 180).");
        }
    }

    private static IReadOnlyList<string> DetermineActiveFilters(SearchRoomsQuery query)
    {
        var filters = new List<string>();

        if (query.MinPrice.HasValue) filters.Add("MinPrice");
        if (query.MaxPrice.HasValue) filters.Add("MaxPrice");
        if (query.MinArea.HasValue) filters.Add("MinArea");
        if (query.MaxArea.HasValue) filters.Add("MaxArea");
        if (!string.IsNullOrWhiteSpace(query.RoomType)) filters.Add("RoomType");
        if (!string.IsNullOrWhiteSpace(query.Keyword)) filters.Add("Keyword");
        if (query.Latitude.HasValue) filters.Add("Latitude");
        if (query.Longitude.HasValue) filters.Add("Longitude");
        if (query.RadiusInKm.HasValue) filters.Add("RadiusInKm");

        return filters;
    }

    private static double? CalculateHaversineDistanceKm(
        double? userLat,
        double? userLon,
        double? roomLat,
        double? roomLon)
    {
        if (!userLat.HasValue || !userLon.HasValue || !roomLat.HasValue || !roomLon.HasValue)
        {
            return null;
        }

        const double EarthRadiusKm = 6371.0;

        var dLat = DegreesToRadians(roomLat.Value - userLat.Value);
        var dLon = DegreesToRadians(roomLon.Value - userLon.Value);

        var rUserLat = DegreesToRadians(userLat.Value);
        var rRoomLat = DegreesToRadians(roomLat.Value);

        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0) * Math.Cos(rUserLat) * Math.Cos(rRoomLat);
        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

        var distance = EarthRadiusKm * c;
        return Math.Round(distance, 2);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
