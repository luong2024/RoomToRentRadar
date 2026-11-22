using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Ports.Models.Listings;

public sealed record ListingQuery(
    int Limit,
    string? Cursor,
    ListingSort Sort,
    ListingFilters Filters);
