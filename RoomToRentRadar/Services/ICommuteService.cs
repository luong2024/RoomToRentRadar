using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public interface ICommuteService
{
    IReadOnlyList<CampusDestinationOption> GetCampusDestinations();

    Task<CommuteEstimateDto> GetCommuteEstimateAsync(
        string listingId,
        CampusDestinationOption campus,
        double? roomLat = null,
        double? roomLng = null,
        bool forceFallback = false,
        CancellationToken cancellationToken = default);
}
