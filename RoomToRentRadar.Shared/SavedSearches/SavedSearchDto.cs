using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Shared.SavedSearches;

public sealed record SavedSearchDto(
    Guid Id,
    string Name,
    SavedSearchAlertState AlertState,
    AreaPolygon Area,
    DateTime CreatedAt,
    DateTime UpdatedAt);
