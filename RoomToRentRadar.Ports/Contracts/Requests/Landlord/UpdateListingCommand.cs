using RoomToRentRadar.Ports.Contracts.Requests.Listings;

namespace RoomToRentRadar.Ports.Contracts.Requests.Landlord;

/// <summary>
/// Input of <c>operationId: updateListing</c> (PUT /api/landlord/rooms/{roomId}, UC-32).
/// <see cref="IfMatch"/> is the raw <c>If-Match</c> header ("&lt;version&gt;"); a missing value must
/// surface as 428 IF_MATCH_REQUIRED and a stale value as 412 ETAG_MISMATCH (ADR-012).
/// </summary>
public sealed record UpdateListingCommand(
    string LandlordId,
    string RoomId,
    string? IfMatch,
    UpsertListingRequest Listing);
