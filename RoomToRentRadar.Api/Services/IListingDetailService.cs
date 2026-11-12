using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services;

public interface IListingDetailService
{
    Task<ListingDetailDto?> GetByIdAsync(
        string id,
        bool isAuthenticated,
        CancellationToken cancellationToken = default);
}
