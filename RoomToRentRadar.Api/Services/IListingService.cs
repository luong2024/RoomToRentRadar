using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services;

public interface IListingService
{
    Task<PagedResult<ListingCardDto>> GetListingsAsync(
        ListingQueryRequest request,
        CancellationToken cancellationToken = default);
}
