using System.Net;
using System.Net.Http.Json;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Infrastructure.Api;

public sealed class ApiClient(HttpClient httpClient) : IApiClient
{
    public async Task<PagedResult<ListingSummaryDto>> GetListingsAsync(
        int limit,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/listings?limit={limit}";
        if (!string.IsNullOrEmpty(cursor))
        {
            url += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        var result = await httpClient.GetFromJsonAsync<PagedResult<ListingSummaryDto>>(
            url,
            cancellationToken);

        return result ?? throw new InvalidOperationException("The listings endpoint returned an empty response.");
    }

    public async Task<ListingDetailFetchResult> GetListingDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/v1/listings/{id}", cancellationToken);

        // 404 and 410 are expected UI states for a listing detail, not failures.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return new ListingDetailFetchResult(ListingDetailFetchOutcome.NotFound, null);

        if (response.StatusCode == HttpStatusCode.Gone)
            return new ListingDetailFetchResult(ListingDetailFetchOutcome.Unavailable, null);

        response.EnsureSuccessStatusCode();

        var detail = await response.Content.ReadFromJsonAsync<ListingDetailDto>(
            ApiJson.Options,
            cancellationToken);

        return detail is null
            ? throw new InvalidOperationException("The listing detail endpoint returned an empty response.")
            : new ListingDetailFetchResult(ListingDetailFetchOutcome.Found, detail);
    }

    public async Task<IReadOnlyList<ListingSummaryDto>> GetSimilarListingsAsync(
        Guid id,
        int limit = 4,
        CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<List<ListingSummaryDto>>(
            $"api/v1/listings/{id}/similar?limit={limit}",
            ApiJson.Options,
            cancellationToken);

        return result ?? throw new InvalidOperationException(
            "The similar listings endpoint returned an empty response.");
    }

    public async Task<ListingContactFetchResult> GetListingContactAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/v1/listings/{id}/contact", cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                return new ListingContactFetchResult(ListingContactFetchOutcome.Unauthorized, null);
            case HttpStatusCode.NotFound:
                return new ListingContactFetchResult(ListingContactFetchOutcome.NotFound, null);
            case HttpStatusCode.Gone:
                return new ListingContactFetchResult(ListingContactFetchOutcome.Unavailable, null);
        }

        response.EnsureSuccessStatusCode();

        var contact = await response.Content.ReadFromJsonAsync<ListingContactDto>(
            ApiJson.Options,
            cancellationToken);

        return contact is null
            ? throw new InvalidOperationException("The listing contact endpoint returned an empty response.")
            : new ListingContactFetchResult(ListingContactFetchOutcome.Found, contact);
    }

    public async Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/v1/auth/login",
            request,
            ApiJson.Options,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(
            ApiJson.Options,
            cancellationToken);

        return login ?? throw new InvalidOperationException("The login endpoint returned an empty response.");
    }

    public async Task<LoginResponse?> DemoLoginAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(
            "api/v1/auth/demo-login",
            content: null,
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(
            ApiJson.Options,
            cancellationToken);

        return login ?? throw new InvalidOperationException("The demo-login endpoint returned an empty response.");
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync("api/v1/auth/logout", null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }
}
