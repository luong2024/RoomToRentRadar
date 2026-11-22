using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Notifications;
using RoomToRentRadar.Shared.SavedSearches;

namespace RoomToRentRadar.Services.Api;

public interface IApiClient
{
    Task<ListingSearchResultDto> GetListingsAsync(
        int limit,
        string? cursor = null,
        ActiveListingFiltersDto? filters = null,
        CancellationToken cancellationToken = default);

    Task<ListingDetailFetchResult> GetListingDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingSummaryDto>> GetSimilarListingsAsync(
        Guid id,
        int limit = 4,
        CancellationToken cancellationToken = default);

    Task<ListingContactFetchResult> GetListingContactAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<LoginResponse?> DemoLoginAsync(CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task<SearchResultDto<ListingSummaryDto>> SearchByAreaAsync(
        AreaSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<SavedSearchDto> CreateSavedSearchAsync(
        CreateSavedSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns null when the saved search no longer exists on the server.</summary>
    Task<SavedSearchDto?> SetSavedSearchAlertAsync(
        Guid id,
        SavedSearchAlertState alertState,
        CancellationToken cancellationToken = default);

    Task DeleteSavedSearchAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default);
}
