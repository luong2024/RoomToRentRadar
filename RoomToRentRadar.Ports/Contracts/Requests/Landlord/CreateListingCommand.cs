namespace RoomToRentRadar.Ports.Contracts.Requests.Landlord;

/// <summary>Input of <c>operationId: createListing</c> (POST /api/landlord/rooms, UC-30).</summary>
public sealed record CreateListingCommand(string LandlordId, UpsertListingRequest Listing);
