using System.Net.Http.Json;
using System.Text.Json;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Infrastructure.Api;

public sealed class ApiClient(HttpClient httpClient) : IApiClient
{
    public async Task<PagedResult<ListingSummaryDto>> GetListingsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<PagedResult<ListingSummaryDto>>(
            $"api/v1/listings?limit={limit}",
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
