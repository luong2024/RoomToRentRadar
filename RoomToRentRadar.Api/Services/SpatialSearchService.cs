using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services.Geo;
using RoomToRentRadar.Api.Services.Mapping;
using RoomToRentRadar.Api.Services.Search;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services;

public interface ISpatialSearchService
{
    /// <summary>Trả về tin đăng có vị trí nằm trong polygon (GeoJSON ring [lng, lat]).</summary>
    /// <exception cref="RequestValidationException">Polygon không hợp lệ.</exception>
    Task<SpatialSearchResponseDto> SearchInPolygonAsync(
        SpatialSearchRequestDto request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US05 / T05.2 — lọc tin đăng theo vùng vẽ trên bản đồ.
/// Hình học nằm ở <see cref="GeoPolygon"/>, sắp xếp ở <see cref="ListingSorter"/>, map ở <see cref="ListingPinMapper"/>.
/// Khi chuyển sang Cosmos DB có thể thay bằng ST_WITHIN trong repository.
/// </summary>
public sealed class SpatialSearchService(IListingRepository repository) : ISpatialSearchService
{
    public async Task<SpatialSearchResponseDto> SearchInPolygonAsync(
        SpatialSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ring = GeoPolygon.Normalize(request.Polygon);
        var listings = await repository.GetPublicListingsAsync(CampusIds.OrDefault(request.CampusId), cancellationToken);

        var inside = listings.Where(l => GeoPolygon.Contains(ring, l.Location.Coordinates));
        var items = ListingSorter.Apply(inside, request.SortBy).Select(ListingPinMapper.ToPin).ToList();

        return new SpatialSearchResponseDto { TotalCount = items.Count, Items = items };
    }
}
