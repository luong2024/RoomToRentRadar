using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public sealed record RoomDetailFetchResult(ListingDetailContract? Detail, bool IsFromLiveApi);

public interface IRoomDetailService
{
    Task<RoomDetailFetchResult> GetRoomDetailAsync(
        string roomId,
        bool isAuthenticated = false,
        CancellationToken cancellationToken = default);
}
