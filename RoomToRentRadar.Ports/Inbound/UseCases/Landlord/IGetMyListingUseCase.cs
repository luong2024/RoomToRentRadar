using RoomToRentRadar.Ports.Contracts.Responses.Listings;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Landlord;

/// <summary>
/// Returns one listing owned by the caller plus its <c>ETag</c> version (UC-31, anti-IDOR:
/// another landlord's listing is a 404 ROOM_NOT_FOUND).
/// </summary>
public interface IGetMyListingUseCase
{
    Task<MyListingDetailDto> ExecuteAsync(string landlordId, string roomId, CancellationToken ct = default);
}
