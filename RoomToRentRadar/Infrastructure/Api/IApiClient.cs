using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Infrastructure.Api;

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
}
