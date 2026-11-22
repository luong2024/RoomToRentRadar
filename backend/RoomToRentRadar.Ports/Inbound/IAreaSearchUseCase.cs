using RoomToRentRadar.Ports.Models.Listings;

namespace RoomToRentRadar.Ports.Inbound;

public interface IAreaSearchUseCase
{
    Task<AreaSearchResult> SearchAsync(
        AreaSearchQuery query,
        CancellationToken cancellationToken);
}
