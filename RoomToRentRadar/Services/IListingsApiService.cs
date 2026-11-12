using System.Threading;
using System.Threading.Tasks;
using Refit;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public interface IListingsApiService
{
    [Get("/api/v1/listings")]
    Task<PagedResult<ListingCardDto>> GetListingsAsync(
        [Query] string campusId = "ictu-main",
        [Query] int pageIndex = 1,
        [Query] int pageSize = 20,
        [Query] string sortBy = "distance",
        CancellationToken cancellationToken = default);
}