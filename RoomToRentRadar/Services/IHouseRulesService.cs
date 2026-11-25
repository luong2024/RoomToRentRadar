using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public sealed record HouseRulesUpdateResult(HouseRulesResponseContract Rules, bool IsFromLiveApi);

public interface IHouseRulesService
{
    Task<HouseRulesUpdateResult> UpdateHouseRulesAsync(
        string listingId,
        UpdateHouseRulesRequestContract request,
        HouseRulesResponseContract? currentState = null,
        CancellationToken cancellationToken = default);
}
