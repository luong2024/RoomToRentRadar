using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;

namespace RoomToRentRadar.Client.Adapters.Http;

public sealed class HttpCampusGateway : ICampusGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpCampusGateway>? _logger;

    public HttpCampusGateway(HttpClient httpClient, ILogger<HttpCampusGateway>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CampusDto>> ListCampusesAsync(CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync("/api/campuses", ct);
        return await ApiResponseReader.ReadAsync<List<CampusDto>>(response, _logger, ct);
    }
}