using RoomToRentRadar.Api.DTOs;

namespace RoomToRentRadar.Api.Services;

public interface IListingService
{
    Task<PagedResponse<ListingDto>> GetListingsAsync(ListingQueryParameters parameters, CancellationToken cancellationToken = default);
}
