using System.Threading;
using System.Threading.Tasks;
using Refit;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public interface ILandlordListingApiService
{
    [Post("/api/v1/landlord/listings")]
    Task<CreateListingResponseContract> CreateListingAsync(
        [Body] CreateListingRequestContract request,
        CancellationToken cancellationToken = default);
}
