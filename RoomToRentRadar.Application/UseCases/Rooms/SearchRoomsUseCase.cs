using System.Globalization;
using System.Text;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Rooms;

public sealed class SearchRoomsUseCase : ISearchRoomsUseCase
{
    public const double IctuLatitude = 21.5851;
    public const double IctuLongitude = 105.8066;

    private const string PriceValidationMessage = "MinPrice cannot be greater than MaxPrice or negative.";
    private const string AreaValidationMessage = "MinArea cannot be greater than MaxArea or negative.";
    private const string MaxDistanceValidationMessage = "MaxDistanceKm must be greater than 0.";

    private readonly IRoomRepository _repository;

    public SearchRoomsUseCase(IRoomRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<PagedResult<RoomCardDto>> ExecuteAsync(RoomSearchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        var originLat = request.Latitude ?? IctuLatitude;
        var originLng = request.Longitude ?? IctuLongitude;
        var keyword = NormalizeForSearch(request.Keyword);

        var rooms = await _repository.GetAllAsync(ct);

        var filtered = (rooms ?? Array.Empty<Room>())
            .Where(room => room.IsVerified && room.IsAvailable)
            .Where(room => !request.MinPrice.HasValue || room.Price >= request.MinPrice.Value)
            .Where(room => !request.MaxPrice.HasValue || room.Price <= request.MaxPrice.Value)
            .Where(room => !request.MinArea.HasValue || room.Area >= request.MinArea.Value)
            .Where(room => !request.MaxArea.HasValue || room.Area <= request.MaxArea.Value)
            .Select(room => new SearchHit(
                room,
                HaversineDistanceCalculator.CalculateDistanceKm(originLat, originLng, room.Latitude, room.Longitude)))
            .Where(hit => !request.MaxDistanceKm.HasValue || hit.DistanceKm <= request.MaxDistanceKm.Value)
            .Where(hit => keyword.Length == 0
                || NormalizeForSearch(hit.Room.Title).Contains(keyword, StringComparison.Ordinal)
                || NormalizeForSearch(hit.Room.Address).Contains(keyword, StringComparison.Ordinal))
            .ToList();

        var totalCount = filtered.Count;
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Max(1, request.PageSize);

        // Tính offset bằng long để tránh tràn int khi page rất lớn (ví dụ page = int.MaxValue).
        var skip = (long)(page - 1) * pageSize;
        var items = skip >= totalCount
            ? new List<RoomCardDto>()
            : Sort(filtered, request.SortBy)
                .Skip((int)skip)
                .Take(pageSize)
                .Select(hit => ToDto(hit.Room, hit.DistanceKm))
                .ToList();

        return new PagedResult<RoomCardDto>(items, totalCount, page, pageSize);
    }

    private static void Validate(RoomSearchRequest request)
    {
        if ((request.MinPrice.HasValue && (request.MinPrice.Value < 0m || (request.MaxPrice.HasValue && request.MinPrice.Value > request.MaxPrice.Value)))
            || (request.MaxPrice.HasValue && request.MaxPrice.Value < 0m))
        {
            throw new DomainValidationException(PriceValidationMessage);
        }

        if ((request.MinArea.HasValue && (request.MinArea.Value < 0d || (request.MaxArea.HasValue && request.MinArea.Value > request.MaxArea.Value)))
            || (request.MaxArea.HasValue && request.MaxArea.Value < 0d))
        {
            throw new DomainValidationException(AreaValidationMessage);
        }

        if (request.MaxDistanceKm.HasValue && request.MaxDistanceKm.Value <= 0d)
        {
            throw new DomainValidationException(MaxDistanceValidationMessage);
        }
    }

    private static IEnumerable<SearchHit> Sort(IEnumerable<SearchHit> hits, string? sortBy)
    {
        var normalizedSortBy = sortBy?.Trim().ToLowerInvariant();

        return normalizedSortBy switch
        {
            "price_asc" => hits.OrderBy(hit => hit.Room.Price).ThenBy(hit => hit.DistanceKm),
            "price_desc" => hits.OrderByDescending(hit => hit.Room.Price).ThenBy(hit => hit.DistanceKm),
            "area_asc" => hits.OrderBy(hit => hit.Room.Area).ThenBy(hit => hit.DistanceKm),
            "area_desc" => hits.OrderByDescending(hit => hit.Room.Area).ThenBy(hit => hit.DistanceKm),
            "newest" => hits.OrderByDescending(hit => hit.Room.CreatedAtUtc).ThenBy(hit => hit.DistanceKm),
            _ => hits.OrderBy(hit => hit.DistanceKm)
        };
    }

    private static RoomCardDto ToDto(Room room, double distanceKm) => new(
        Id: room.Id,
        Title: room.Title,
        Description: room.Description,
        Price: room.Price,
        Area: room.Area,
        Address: room.Address,
        Latitude: room.Latitude,
        Longitude: room.Longitude,
        DistanceKm: distanceKm,
        IsAvailable: room.IsAvailable,
        IsVerified: room.IsVerified,
        CreatedAtUtc: room.CreatedAtUtc);

    /// <summary>
    /// Chuẩn hóa chuỗi để tìm kiếm: bỏ dấu tiếng Việt, chuyển chữ thường và gộp khoảng trắng.
    /// Dùng FormD rồi loại bỏ các ký tự Unicode NonSpacingMark (U+0300–U+036F) và ký tự "đ"/"Đ"
    /// (không tồn tại dạng tổ hợp trong Unicode nên phải map tay).
    /// </summary>
    internal static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lower = value.Trim().ToLowerInvariant();
        var decomposed = lower.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (character == 'đ')
            {
                builder.Append('d');
                continue;
            }

            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private readonly record struct SearchHit(Room Room, double DistanceKm);
}
