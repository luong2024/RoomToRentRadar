using System.Globalization;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadar.Client.Adapters.Http;

public sealed class HttpRoomGateway : IRoomGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpRoomGateway>? _logger;

    public HttpRoomGateway(HttpClient httpClient, ILogger<HttpRoomGateway>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PagedResult<RoomCardDto>> SearchRoomsAsync(RoomSearchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var response = await _httpClient.GetAsync(BuildSearchUrl(request), ct);
        return await ApiResponseReader.ReadAsync<PagedResult<RoomCardDto>>(response, _logger, ct);
    }

    public Task<AreaSearchResultDto> SearchRoomsInAreaAsync(AreaSearchRequest request, CancellationToken ct = default) =>
        Task.FromException<AreaSearchResultDto>(new NotImplementedException("T05.1: searchRoomsInArea is not implemented yet"));

    public Task<ComparisonDto> CompareRoomsAsync(IReadOnlyList<string> roomIds, string? campusId = null, CancellationToken ct = default) =>
        Task.FromException<ComparisonDto>(new NotImplementedException("T10.1: compareRooms is not implemented yet"));

    public Task<RoomDetailDto> GetRoomDetailAsync(string roomId, string? campusId = null, CancellationToken ct = default) =>
        Task.FromException<RoomDetailDto>(new NotImplementedException("T02.1: getRoomDetail is not implemented yet"));

    public Task<IReadOnlyList<RoomCardDto>> GetSimilarRoomsAsync(string roomId, string? campusId = null, CancellationToken ct = default) =>
        Task.FromException<IReadOnlyList<RoomCardDto>>(new NotImplementedException("T02.1: getSimilarRooms is not implemented yet"));

    public Task<TravelTimesDto> GetRoomTravelTimesAsync(string roomId, string? campusId = null, CancellationToken ct = default) =>
        Task.FromException<TravelTimesDto>(new NotImplementedException("T09.1: getRoomTravelTimes is not implemented yet"));

    private static string BuildSearchUrl(RoomSearchRequest request)
    {
        var query = new List<string>(10);
        if (request.CampusId is not null)
        {
            query.Add($"campusId={Uri.EscapeDataString(request.CampusId)}");
        }

        if (request.MinRentVnd is { } minRentVnd)
        {
            query.Add($"minRentVnd={minRentVnd.ToString(CultureInfo.InvariantCulture)}");
        }

        if (request.MaxRentVnd is { } maxRentVnd)
        {
            query.Add($"maxRentVnd={maxRentVnd.ToString(CultureInfo.InvariantCulture)}");
        }

        if (request.MinAreaM2 is { } minAreaM2)
        {
            query.Add($"minAreaM2={minAreaM2.ToString(CultureInfo.InvariantCulture)}");
        }

        if (request.MaxAreaM2 is { } maxAreaM2)
        {
            query.Add($"maxAreaM2={maxAreaM2.ToString(CultureInfo.InvariantCulture)}");
        }

        if (request.RoomTypes is not null)
        {
            query.Add($"roomTypes={Uri.EscapeDataString(string.Join(",", request.RoomTypes.Select(t => t.ToString())))}");
        }

        if (request.MaxDistanceKm is { } maxDistanceKm)
        {
            query.Add($"maxDistanceKm={maxDistanceKm.ToString(CultureInfo.InvariantCulture)}");
        }

        // Present-but-empty sort must still be sent: the server rejects it instead of applying the default.
        if (request.Sort is not null)
        {
            query.Add($"sort={Uri.EscapeDataString(request.Sort)}");
        }

        query.Add($"pageNumber={request.PageNumber.ToString(CultureInfo.InvariantCulture)}");
        query.Add($"pageSize={request.PageSize.ToString(CultureInfo.InvariantCulture)}");
        return $"/api/rooms?{string.Join("&", query)}";
    }
}