using System.Net.Http.Json;
using System.Text.Json;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Services.Api;

public sealed class ProfileClient(HttpClient httpClient) : IProfileClient
{
    public async Task<UserProfileDto> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/v1/auth/me", cancellationToken);
        return await ReadProfileAsync(response, cancellationToken);
    }

    public async Task<UserProfileDto> UpdateDisplayNameAsync(
        string displayName,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            "api/v1/auth/me",
            new UpdateProfileRequest(displayName),
            ApiJson.Options,
            cancellationToken);

        return await ReadProfileAsync(response, cancellationToken);
    }

    private static async Task<UserProfileDto> ReadProfileAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<UserProfileDto>(
            ApiJson.Options,
            cancellationToken)
            ?? throw new InvalidOperationException("The profile endpoint returned an empty response.");
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
