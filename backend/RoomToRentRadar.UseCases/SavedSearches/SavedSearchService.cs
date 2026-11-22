using System.Text.Json;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.SavedSearches;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.UseCases.SavedSearches;

public sealed class SavedSearchService(
    ISavedSearchRepository savedSearchRepository,
    IUnitOfWork unitOfWork) : ISavedSearchesUseCase
{
    public async Task<SavedSearchDto> CreateAsync(
        Guid userId,
        string name,
        AreaPolygon area,
        SavedSearchAlertState alertState,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var savedSearch = new SavedSearch
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            AreaJson = JsonSerializer.Serialize(area, ApiJson.Options),
            AlertState = alertState,
            AlertsActivatedAt = alertState == SavedSearchAlertState.Active ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        await savedSearchRepository.AddAsync(savedSearch, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(savedSearch);
    }

    public async Task<SavedSearchDto?> SetAlertStateAsync(
        Guid userId,
        Guid savedSearchId,
        SavedSearchAlertState alertState,
        CancellationToken cancellationToken)
    {
        var savedSearch = await savedSearchRepository.FindAsync(userId, savedSearchId, cancellationToken);
        if (savedSearch is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        if (alertState == SavedSearchAlertState.Active
            && savedSearch.AlertState != SavedSearchAlertState.Active)
        {
            // Resuming must not alert for listings published while the alert was
            // paused, so the trigger watermark restarts now.
            savedSearch.AlertsActivatedAt = now;
        }

        savedSearch.AlertState = alertState;
        savedSearch.UpdatedAt = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(savedSearch);
    }

    public async Task<bool> DeleteAsync(
        Guid userId,
        Guid savedSearchId,
        CancellationToken cancellationToken)
    {
        var savedSearch = await savedSearchRepository.FindAsync(userId, savedSearchId, cancellationToken);
        if (savedSearch is null)
        {
            return false;
        }

        savedSearchRepository.Remove(savedSearch);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static SavedSearchDto ToDto(SavedSearch savedSearch)
    {
        var area = JsonSerializer.Deserialize<AreaPolygon>(savedSearch.AreaJson, ApiJson.Options)!;
        return new SavedSearchDto(
            savedSearch.Id,
            savedSearch.Name,
            savedSearch.AlertState,
            area,
            savedSearch.CreatedAt,
            savedSearch.UpdatedAt);
    }
}
