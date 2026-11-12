using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

public sealed record ListingQuery(
    int Limit,
    string? Cursor,
    ListingSort Sort,
    ListingFilters Filters);
