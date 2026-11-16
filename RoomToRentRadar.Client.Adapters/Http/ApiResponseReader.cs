using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;

namespace RoomToRentRadar.Client.Adapters.Http;

/// <summary>
/// Shared response handling for every HTTP gateway: success bodies deserialize with <see cref="JsonOptions"/>,
/// non-2xx responses become <see cref="ApiException"/> parsed from application/problem+json,
/// and network failures map through <see cref="FromNetworkException"/>.
/// </summary>
public static class ApiResponseReader
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Throws for every non-2xx status, including 304 — cache decorators must check 304 first (ADR-004).</summary>
    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, ILogger? logger = null, CancellationToken ct = default)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, logger, ct);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ApiException("Server returned an empty body.", response.StatusCode);
        }

        var result = JsonSerializer.Deserialize<T>(body, JsonOptions);
        if (result == null)
        {
            throw new ApiException("Server returned an empty body.", response.StatusCode);
        }

        return result;
    }

    public static async Task EnsureSuccessAsync(HttpResponseMessage response, ILogger? logger = null, CancellationToken ct = default)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, logger, ct);
        }
    }

    public static async Task<ApiException> CreateExceptionAsync(HttpResponseMessage response, ILogger? logger = null, CancellationToken ct = default)
    {
        string? errorCode = null;
        string? detail = null;
        Dictionary<string, string[]>? errors = null;

        try
        {
            var rawBody = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(rawBody))
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;

                if (root.TryGetProperty("errorCode", out var codeElem) && codeElem.ValueKind == JsonValueKind.String)
                {
                    errorCode = codeElem.GetString();
                }
                else if (root.TryGetProperty("extensions", out var extElem) && extElem.ValueKind == JsonValueKind.Object
                    && extElem.TryGetProperty("errorCode", out var extCodeElem) && extCodeElem.ValueKind == JsonValueKind.String)
                {
                    errorCode = extCodeElem.GetString();
                }

                if (root.TryGetProperty("detail", out var detailElem) && detailElem.ValueKind == JsonValueKind.String)
                {
                    detail = detailElem.GetString();
                }
                else if (root.TryGetProperty("title", out var titleElem) && titleElem.ValueKind == JsonValueKind.String)
                {
                    detail = titleElem.GetString();
                }

                if (root.TryGetProperty("errors", out var errorsElem) && errorsElem.ValueKind == JsonValueKind.Object)
                {
                    errors = new Dictionary<string, string[]>();
                    foreach (var prop in errorsElem.EnumerateObject())
                    {
                        if (prop.Value.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }

                        var list = new List<string>();
                        foreach (var item in prop.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                list.Add(item.GetString()!);
                            }
                        }
                        errors[prop.Name] = list.ToArray();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to parse API error response body as JSON ProblemDetails");
        }

        var message = detail ?? errorCode ?? $"HTTP error {(int)response.StatusCode} ({response.ReasonPhrase})";
        logger?.LogWarning("API call failed with status {StatusCode}, errorCode {ErrorCode}: {Message}",
            response.StatusCode, errorCode, message);

        return new ApiException(message, response.StatusCode, errorCode, errors);
    }

    public static ApiException FromNetworkException(Exception inner) =>
        new(
            "Unable to connect to the server. Please check your internet connection.",
            statusCode: 0,
            errorCode: ApiErrorCodes.NetworkError,
            innerException: inner);
}
