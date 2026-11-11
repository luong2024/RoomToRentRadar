using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

public sealed record ListingContactResult(ListingDetailOutcome Outcome, ListingContactDto? Contact);
