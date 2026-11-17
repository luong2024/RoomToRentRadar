using RoomToRentRadar.Ports.Contracts.Requests.SavedSearches;
using RoomToRentRadar.Ports.Contracts.Responses.SavedSearches;

namespace RoomToRentRadar.Client.Adapters.Abstractions;

/// <summary>Saved searches and alerts (SavedSearches tag of <c>docs/api/openapi.yaml</c>, US07, BR-SRCH-08, BR-ALR-01).</summary>
public interface ISavedSearchGateway
{
    [ApiOperation("listMySavedSearches")]
    Task<IReadOnlyList<SavedSearchDto>> ListMySavedSearchesAsync(CancellationToken ct = default);

    [ApiOperation("createSavedSearch")]
    Task<SavedSearchDto> CreateSavedSearchAsync(CreateSavedSearchRequest request, CancellationToken ct = default);

    [ApiOperation("updateSavedSearch")]
    Task<SavedSearchDto> UpdateSavedSearchAsync(string savedSearchId, UpdateSavedSearchRequest request, CancellationToken ct = default);

    [ApiOperation("deleteSavedSearch")]
    Task DeleteSavedSearchAsync(string savedSearchId, CancellationToken ct = default);

    [ApiOperation("setSavedSearchAlert")]
    Task<SavedSearchDto> SetSavedSearchAlertAsync(string savedSearchId, SetAlertStateRequest request, CancellationToken ct = default);
}