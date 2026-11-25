using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Requests.Profile;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Client.Adapters.Http;

public sealed class HttpProfileGateway : IProfileGateway
{
    // openapi UpdateMyProfileRequest: only provided fields are sent.
    private static readonly JsonSerializerOptions PatchOptions = new(ApiResponseReader.JsonOptions)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpProfileGateway>? _logger;

    public HttpProfileGateway(HttpClient httpClient, ILogger<HttpProfileGateway>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    public async Task<UserProfileDto> UpdateMyProfileAsync(UpdateMyProfileRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var message = new HttpRequestMessage(HttpMethod.Patch, "/api/me/profile")
        {
            Content = JsonContent.Create(request, options: PatchOptions)
        };
        using var response = await _httpClient.SendAsync(message, ct);
        return await ApiResponseReader.ReadAsync<UserProfileDto>(response, _logger, ct);
    }
}
