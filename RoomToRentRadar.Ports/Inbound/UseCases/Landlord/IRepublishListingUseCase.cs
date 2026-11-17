using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Responses.Landlord;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Landlord;

/// <summary><c>operationId: republishListing</c>: Unpublished → Available (UC-33, BR-LST-04).</summary>
public interface IRepublishListingUseCase
{
    Task<MyListingDetailDto> ExecuteAsync(TransitionMyListingCommand command, CancellationToken ct = default);
}
