using System.Net.Http.Json;
using System.Text.Json;
using RoomToRentRadar.Shared.Moderation;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Services.Api;

public sealed class ModerationClient(HttpClient httpClient) : IModerationClient
{
    public async Task<ModerationListingQueueDto> GetPendingListingsAsync(
        int limit = 20,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/moderation/listings?status=pending_review&limit={limit}";
        if (!string.IsNullOrEmpty(cursor))
        {
            url += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await SendAsync<ModerationListingQueueDto>(request, cancellationToken);
    }

    public async Task ApproveAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/v1/moderation/listings/{listingId}/approve");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(response, cancellationToken);
        }
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, cancellationToken)
            ?? throw new InvalidOperationException("The moderation endpoint returned an empty response.");
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
