using System.Threading;
using System.Threading.Tasks;
using Refit;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public interface IRoomDetailApiService
{
    [Get("/api/v1/listings/{id}")]
    Task<ListingDetailContract> GetListingDetailAsync(
        string id,
        CancellationToken cancellationToken = default);
}
