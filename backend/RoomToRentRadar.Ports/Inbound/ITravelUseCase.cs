using RoomToRentRadar.Ports.Models.Common;
using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.Ports.Inbound;

public interface ITravelUseCase
{
    Task<UseCaseResult<ListingTravelEstimatesDto>> GetEstimatesAsync(
        Guid listingId,
        string campusId,
        double campusLat,
        double campusLng,
        CancellationToken cancellationToken);
}
