using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public sealed record CreateListingSubmitResult(CreateListingResponseContract Response, bool IsFromLiveApi);

public interface ILandlordListingService
{
    Task<CreateListingSubmitResult> SubmitListingAsync(
        CreateListingRequestContract request,
        CancellationToken cancellationToken = default);
}
