using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Responses.Landlord;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Landlord;

/// <summary>
/// Edits a listing the caller owns (UC-32, BR-LST-05/09) with mandatory <c>If-Match</c>
/// (428/412, ADR-012).
/// </summary>
public interface IUpdateListingUseCase
{
    Task<MyListingDetailDto> ExecuteAsync(UpdateListingCommand command, CancellationToken ct = default);
}
