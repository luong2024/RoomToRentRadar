using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Responses.Landlord;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Landlord;

/// <summary><c>operationId: markListingRented</c>: Available → Rented (UC-33, BR-LST-04).</summary>
public interface IMarkListingRentedUseCase
{
    Task<MyListingDetailDto> ExecuteAsync(TransitionMyListingCommand command, CancellationToken ct = default);
}
