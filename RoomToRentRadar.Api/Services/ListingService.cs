using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services;

public sealed class ListingService(IListingRepository repository) : IListingService
{
    public async Task<PagedResult<ListingCardDto>> GetListingsAsync(
        ListingQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = request.EffectivePage;
        var pageSize = Math.Clamp(request.PageSize, 1, ListingQueryRequest.MaxPageSize);
        request.PageNumber = pageNumber;
        request.PageSize = pageSize;

        var (items, totalCount) = await repository.GetPagedAsync(request, cancellationToken);
        var cards = items.Select(ToCard).ToList();

        return PagedResult<ListingCardDto>.Create(cards, totalCount, pageNumber, pageSize);
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
            ThumbnailUrl = listing.Photos
                .Where(p => !p.Is360)
                .OrderBy(p => p.Order)
                .Select(p => p.Url)
                .FirstOrDefault() ?? string.Empty,
            Status = listing.Status,
            IsVerified = listing.IsVerified
        };
}
