using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Infrastructure.Api;

public sealed class ApiClient(HttpClient httpClient) : IApiClient
{
    public async Task<ListingSearchResultDto> GetListingsAsync(
        int limit,
        string? cursor = null,
        ActiveListingFiltersDto? filters = null,
        CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<ListingSearchResultDto>(
            BuildListingsUrl(limit, cursor, filters),
            ApiJson.Options,
            cancellationToken);

        return result ?? throw new InvalidOperationException("The listings endpoint returned an empty response.");
    }

    public async Task<SearchResultDto<ListingSummaryDto>> SearchByAreaAsync(
        AreaSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/v1/listings/search/area",
            request,
            ApiJson.Options,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(response, cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<SearchResultDto<ListingSummaryDto>>(
            ApiJson.Options,
            cancellationToken);

        return result ?? throw new InvalidOperationException("The area search endpoint returned an empty response.");
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

    private static string BuildListingsUrl(
        int limit,
        string? cursor,
        ActiveListingFiltersDto? filters)
    {
        var url = new StringBuilder($"api/v1/listings?limit={limit}");

        if (!string.IsNullOrEmpty(cursor))
        {
            url.Append("&cursor=").Append(Uri.EscapeDataString(cursor));
        }

        if (filters is null)
            return url.ToString();

        AppendLong(url, "min_price", filters.MinPrice);
        AppendLong(url, "max_price", filters.MaxPrice);
        AppendDouble(url, "min_area_m2", filters.MinAreaM2);
        AppendDouble(url, "max_area_m2", filters.MaxAreaM2);
        AppendLong(url, "bedrooms", filters.Bedrooms);
        if (filters.RoomType is { } roomType)
        {
            url.Append("&room_type=").Append(Uri.EscapeDataString(RoomTypeWire.ToWireValue(roomType)));
        }

        AppendDouble(url, "campus_lat", filters.CampusLat);
        AppendDouble(url, "campus_lng", filters.CampusLng);
        AppendDouble(url, "max_distance_km", filters.MaxDistanceKm);

        return url.ToString();
    }

    private static void AppendLong(StringBuilder url, string name, long? value)
    {
        if (value is { } number)
        {
            url.Append('&').Append(name).Append('=').Append(number.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void AppendDouble(StringBuilder url, string name, double? value)
    {
        if (value is { } number)
        {
            url.Append('&').Append(name).Append('=').Append(number.ToString("R", CultureInfo.InvariantCulture));
        }
    }

    private static async Task<ApiException> CreateApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string? title = null;
        string? detail = null;
        string? code = null;

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                title = ReadString(root, "title");
                detail = ReadString(root, "detail");
                code = ReadString(root, "code");
            }
        }
        catch (JsonException)
        {
        }

        return new ApiException(
            response.StatusCode,
            detail ?? title ?? $"The API returned status {(int)response.StatusCode}.",
            code);
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
