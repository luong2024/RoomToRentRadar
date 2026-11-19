using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Responses.Listings;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Landlord;

/// <summary><c>operationId: markListingAvailable</c>: Rented → Available (UC-33, BR-LST-04).</summary>
public interface IMarkListingAvailableUseCase
{
    Task<MyListingDetailDto> ExecuteAsync(TransitionMyListingCommand command, CancellationToken ct = default);
}
