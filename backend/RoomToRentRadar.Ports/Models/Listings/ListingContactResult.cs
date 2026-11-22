using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Ports.Models.Listings;

public sealed record ListingContactResult(ListingDetailOutcome Outcome, ListingContactDto? Contact);
