using System.Threading;
using System.Threading.Tasks;
using Refit;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public interface IHouseRulesApiService
{
    [Put("/api/v1/listings/{id}/house-rules")]
    Task<HouseRulesResponseContract> UpdateHouseRulesAsync(
        string id,
        [Body] UpdateHouseRulesRequestContract request,
        CancellationToken cancellationToken = default);
}
