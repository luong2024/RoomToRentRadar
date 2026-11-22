using RoomToRentRadar.Shared.SavedSearches;

namespace RoomToRentRadar.Ports.Inbound;

public interface ISavedSearchesUseCase
{
    Task<SavedSearchDto> CreateAsync(
        Guid userId,
        string name,
        Shared.Listings.AreaPolygon area,
        SavedSearchAlertState alertState,
        CancellationToken cancellationToken);

    Task<SavedSearchDto?> SetAlertStateAsync(
        Guid userId,
        Guid savedSearchId,
        SavedSearchAlertState alertState,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid userId, Guid savedSearchId, CancellationToken cancellationToken);
}
