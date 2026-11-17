using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Responses.Landlord;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Landlord;

/// <summary>Lists the caller's listings with per-status counts (UC-31, BR-LST-11).</summary>
public interface IListMyListingsUseCase
{
    Task<PagedMyListingsDto> ExecuteAsync(ListMyListingsRequest request, CancellationToken ct = default);
}
