using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public interface IListingsService
{
    Task<PagedResult<RoomListingItemModel>> GetListingsAsync(
        string campusId,
        int pageIndex,
        int pageSize,
        string sortBy,
        CancellationToken cancellationToken = default);
}