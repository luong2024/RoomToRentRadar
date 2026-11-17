using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Ports.Contracts.Requests.Landlord;

/// <summary>
/// Query parameters of <c>operationId: listMyListings</c> (GET /api/landlord/rooms, UC-31).
/// Always scoped by the authenticated landlord (anti-IDOR, GUARDRAILS §1.5.6).
/// </summary>
public sealed record ListMyListingsRequest(
    string LandlordId,
    ListingStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20);
