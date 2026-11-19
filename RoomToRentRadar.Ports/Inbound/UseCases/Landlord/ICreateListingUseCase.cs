using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Responses.Listings;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Landlord;

/// <summary>
/// Creates a listing in <c>PendingReview</c> with 3–20 standard photos (UC-30, BR-LST-01..04,
/// BR-LST-11, BR-MED-01..03).
/// </summary>
public interface ICreateListingUseCase
{
    Task<MyListingDetailDto> ExecuteAsync(CreateListingCommand command, CancellationToken ct = default);
}
