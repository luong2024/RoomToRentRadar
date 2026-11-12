using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services;

public interface IListingSearchService
{
    Task<SearchResponseDto> SearchAsync(ListingSearchQuery query, CancellationToken cancellationToken = default);
}
