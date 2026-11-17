using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Requests.SavedSearches;
using RoomToRentRadar.Ports.Contracts.Responses.SavedSearches;

namespace RoomToRentRadar.Client.Adapters.Http;

/// <summary>Saved-search endpoints arrive with T07.1; every operation is a task-id stub until then (PLAN §5.4).</summary>
public sealed class HttpSavedSearchGateway : ISavedSearchGateway
{
    public HttpSavedSearchGateway(HttpClient httpClient, ILogger<HttpSavedSearchGateway>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
    }

    public Task<IReadOnlyList<SavedSearchDto>> ListMySavedSearchesAsync(CancellationToken ct = default) =>
        Pending.For<IReadOnlyList<SavedSearchDto>>("T07.1", "listMySavedSearches");

    public Task<SavedSearchDto> CreateSavedSearchAsync(CreateSavedSearchRequest request, CancellationToken ct = default) =>
        Pending.For<SavedSearchDto>("T07.1", "createSavedSearch");

    public Task<SavedSearchDto> UpdateSavedSearchAsync(string savedSearchId, UpdateSavedSearchRequest request, CancellationToken ct = default) =>
        Pending.For<SavedSearchDto>("T07.1", "updateSavedSearch");

    public Task DeleteSavedSearchAsync(string savedSearchId, CancellationToken ct = default) =>
        Pending.For("T07.1", "deleteSavedSearch");

    public Task<SavedSearchDto> SetSavedSearchAlertAsync(string savedSearchId, SetAlertStateRequest request, CancellationToken ct = default) =>
        Pending.For<SavedSearchDto>("T07.1", "setSavedSearchAlert");
}