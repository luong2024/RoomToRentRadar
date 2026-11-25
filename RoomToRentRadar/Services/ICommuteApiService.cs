using System.Threading;
using System.Threading.Tasks;
using Refit;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public interface ICommuteApiService
{
    [Get("/api/v1/listings/{listingId}/commute")]
    Task<CommuteEstimateDto> GetCommuteEstimateAsync(
        string listingId,
        [Query] string campusId = "ictu-main",
        CancellationToken cancellationToken = default);
}
