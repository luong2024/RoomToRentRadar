using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RoomToRentRadar.Models;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Notifications;
using RoomToRentRadar.Shared.SavedSearches;
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

    public async Task<SavedSearchDto> CreateSavedSearchAsync(
        CreateSavedSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/v1/saved-searches",
            request,
            ApiJson.Options,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(response, cancellationToken);

        var savedSearch = await response.Content.ReadFromJsonAsync<SavedSearchDto>(
            ApiJson.Options,
            cancellationToken);

        return savedSearch ?? throw new InvalidOperationException(
            "The saved search endpoint returned an empty response.");
    }

    public async Task<SavedSearchDto?> SetSavedSearchAlertAsync(
        Guid id,
        SavedSearchAlertState alertState,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"api/v1/saved-searches/{id}/alert",
            new SetSavedSearchAlertRequest { AlertState = alertState },
            ApiJson.Options,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(response, cancellationToken);

        var savedSearch = await response.Content.ReadFromJsonAsync<SavedSearchDto>(
            ApiJson.Options,
            cancellationToken);

        return savedSearch ?? throw new InvalidOperationException(
            "The saved search alert endpoint returned an empty response.");
    }

    public async Task DeleteSavedSearchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/v1/saved-searches/{id}", cancellationToken);

        // A row that is already gone is the desired end state.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return;

        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var notifications = await httpClient.GetFromJsonAsync<List<NotificationDto>>(
            $"api/v1/notifications?limit={limit}",
            ApiJson.Options,
            cancellationToken);

        return notifications ?? throw new InvalidOperationException(
            "The notifications endpoint returned an empty response.");
    }

    public async Task<IReadOnlyList<ManagedListingDto>> GetMyListingsAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/v1/listings/mine", cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(response, cancellationToken);

        var listings = await response.Content.ReadFromJsonAsync<List<ManagedListingDto>>(
            ApiJson.Options,
            cancellationToken);

        return listings ?? throw new InvalidOperationException(
            "The managed listings endpoint returned an empty response.");
    }

    public async Task<ManagedListingDto> GetManagedListingAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/v1/listings/{id}/manage", cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(response, cancellationToken);

        return await ReadManagedListingAsync(response, cancellationToken);
    }

    public async Task<ManagedListingDto> CreateListingAsync(
        ListingSubmission submission,
        CancellationToken cancellationToken = default)
    {
        using var content = CreateListingContent(submission);
        await AddPhotosAsync(content, submission.Photos);
        using var response = await httpClient.PostAsync("api/v1/listings", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw await CreateListingSaveExceptionAsync(response, cancellationToken);

        return await ReadManagedListingAsync(response, cancellationToken);
    }

    public async Task<ManagedListingDto> UpdateListingAsync(
        Guid id,
        ListingSubmission submission,
        CancellationToken cancellationToken = default)
    {
        using var content = CreateListingContent(submission);
        await AddPhotosAsync(content, submission.Photos);
        using var response = await httpClient.PutAsync($"api/v1/listings/{id}", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw await CreateListingSaveExceptionAsync(response, cancellationToken);

        return await ReadManagedListingAsync(response, cancellationToken);
    }

    public async Task<ManagedListingDto> UnpublishListingAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(
            $"api/v1/listings/{id}/unpublish",
            content: null,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(response, cancellationToken);

        return await ReadManagedListingAsync(response, cancellationToken);
    }

    private static MultipartFormDataContent CreateListingContent(ListingSubmission submission)
    {
        var content = new MultipartFormDataContent();

        AddText(content, "title", submission.Title);
        AddText(content, "address", submission.Address);
        AddText(content, "price", submission.Price.ToString(CultureInfo.InvariantCulture));
        AddText(content, "deposit", submission.Deposit.ToString(CultureInfo.InvariantCulture));
        AddText(content, "area_m2", submission.AreaM2.ToString("R", CultureInfo.InvariantCulture));
        AddText(content, "room_type", RoomTypeWire.ToWireValue(submission.RoomType));
        AddText(content, "bedrooms", submission.Bedrooms.ToString(CultureInfo.InvariantCulture));
        AddText(content, "description", submission.Description);
        AddText(content, "landlord_name", submission.LandlordName);
        AddText(content, "landlord_phone", submission.LandlordPhone);

        if (submission.Lat is { } lat && double.IsFinite(lat))
            AddText(content, "lat", lat.ToString("R", CultureInfo.InvariantCulture));

        if (submission.Lng is { } lng && double.IsFinite(lng))
            AddText(content, "lng", lng.ToString("R", CultureInfo.InvariantCulture));

        return content;
    }

    private static void AddText(MultipartFormDataContent content, string name, string? value)
        => content.Add(new StringContent(value ?? string.Empty, Encoding.UTF8), name);

    private static async Task AddPhotosAsync(
        MultipartFormDataContent content,
        IReadOnlyList<ListingPhotoUploadModel> photos)
    {
        foreach (var photo in photos)
        {
            // The stream is opened per attempt so a retried upload still has content.
            var stream = await photo.OpenReadAsync();
            var fileContent = new StreamContent(stream);

            if (!string.IsNullOrWhiteSpace(photo.ContentType))
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(photo.ContentType);

            content.Add(fileContent, "photos", SanitizeFileName(photo.FileName));
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var trimmed = string.IsNullOrWhiteSpace(fileName) ? "photo.jpg" : fileName.Trim();
        return trimmed.Replace("\"", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
    }

    private static async Task<ManagedListingDto> ReadManagedListingAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var listing = await response.Content.ReadFromJsonAsync<ManagedListingDto>(
            ApiJson.Options,
            cancellationToken);

        return listing ?? throw new InvalidOperationException(
            "The listing management endpoint returned an empty response.");
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

    private static async Task<ApiException> CreateListingSaveExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string? title = null;
        string? detail = null;
        string? code = null;
        Dictionary<string, IReadOnlyList<string>>? fieldErrors = null;

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
                fieldErrors = ReadFieldErrors(root);
            }
        }
        catch (JsonException)
        {
        }

        return new ListingSaveException(
            response.StatusCode,
            detail ?? title ?? $"The API returned status {(int)response.StatusCode}.",
            code,
            fieldErrors);
    }

    private static Dictionary<string, IReadOnlyList<string>>? ReadFieldErrors(JsonElement root)
    {
        if (!root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
            return null;

        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in errors.EnumerateObject())
        {
            var messages = new List<string>();

            switch (field.Value.ValueKind)
            {
                case JsonValueKind.Array:
                    foreach (var message in field.Value.EnumerateArray())
                    {
                        if (message.ValueKind == JsonValueKind.String && message.GetString() is { Length: > 0 } text)
                            messages.Add(text);
                    }

                    break;

                case JsonValueKind.String:
                    if (field.Value.GetString() is { Length: > 0 } single)
                        messages.Add(single);

                    break;
            }

            if (messages.Count > 0)
                result[field.Name] = messages;
        }

        return result.Count > 0 ? result : null;
    }
}
