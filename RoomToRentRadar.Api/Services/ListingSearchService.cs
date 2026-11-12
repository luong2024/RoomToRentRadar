using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services;

public sealed class ListingSearchService(IListingRepository repository) : IListingSearchService
{
    public async Task<SearchResponseDto> SearchAsync(
        ListingSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await repository.SearchAsync(query, cancellationToken);
        var pageIndex = Math.Max(1, query.PageIndex);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        return new SearchResponseDto
        {
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize,
            HasNextPage = pageIndex * pageSize < totalCount,
            ActiveFilters = new SearchActiveFiltersDto
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
            Items = items.Select(ToCard).ToList()
        };
    }

    private static ListingCardDto ToCard(ListingDocument listing)
        => new()
        {
            Id = listing.Id,
            CampusId = listing.CampusId,
            Title = listing.Title,
            Rent = listing.Rent,
            Area = listing.Area,
            DistanceMeters = listing.DistanceMeters,
            RoomType = listing.RoomType,
            Address = listing.Address,
            ThumbnailUrl = listing.Photos.OrderBy(p => p.Order).Select(p => p.Url).FirstOrDefault() ?? string.Empty,
            Status = listing.Status,
            IsVerified = listing.IsVerified
        };
}
