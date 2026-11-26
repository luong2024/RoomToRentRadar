using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Infrastructure.Fakes;

namespace RoomToRentRadar.Services.Api
{
    /// <summary>
    /// US02 / T02.2: <see cref="IRoomListingRepository"/> for the MAUI frontend, backed by the backend Web API
    /// (RoomListingController, T02.1) instead of a local SQLite file.
    /// - ListVerifiedRoomsAsync → GET /api/rooms (AC1 distance sort, AC4 Available + Verified done by the API).
    /// - Caching (US02 "pagination and caching"): each page is kept with its ETag; the next request sends
    ///   If-None-Match and a 304 reuses the cached page (fast reloads, AC3). Without network the last cached
    ///   page is shown instead of an error.
    /// Partial class: T03.2 (.Detail.cs), T05.2 (.Search.cs) and T09.1 (.Publish.cs) override the other methods
    /// in their own files. Until then they fall back to the in-memory ICTU sample data of the base class.
    /// </summary>
    public partial class ApiRoomListingRepository : InMemoryRoomListingRepository
    {
        private readonly HttpClient _http;
        private readonly ILogger<ApiRoomListingRepository> _logger;

        // Key = request path + query; value = ETag and rooms of the last 200 response.
        private readonly ConcurrentDictionary<string, CachedPage> _pageCache = new();

        public ApiRoomListingRepository(HttpClient http, ILogger<ApiRoomListingRepository> logger)
        {
            _http = http;
            _logger = logger;
        }

        public override async Task<List<RoomListing>> ListVerifiedRoomsAsync(int pageIndex = 0, int pageSize = 20, string sortBy = "DistanceAsc")
        {
            var path = string.Create(CultureInfo.InvariantCulture,
                $"api/rooms?pageIndex={pageIndex}&pageSize={pageSize}&sortBy={Uri.EscapeDataString(sortBy ?? "DistanceAsc")}");

            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            _pageCache.TryGetValue(path, out var cached);
            if (cached is not null)
            {
                request.Headers.IfNoneMatch.Add(cached.ETag);
            }

            try
            {
                using var response = await _http.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.NotModified && cached is not null)
                {
                    return CloneRooms(cached.Rooms);
                }

                if (!response.IsSuccessStatusCode)
                {
                    var message = await ReadErrorMessageAsync(response);
                    _logger.LogWarning("GET {Path} failed with {Status}: {Message}", path, (int)response.StatusCode, message);
                    throw new HttpRequestException(message ?? "Không tải được danh sách phòng.", null, response.StatusCode);
                }

                var page = await response.Content.ReadFromJsonAsync(RoomListingJsonContext.Default.RoomListingPageDto)
                           ?? throw new JsonException("Empty room listing response.");
                var rooms = page.Items.Select(ToRoomListing).ToList();

                if (response.Headers.ETag is { } etag)
                {
                    _pageCache[path] = new CachedPage(etag, rooms);
                }

                return CloneRooms(rooms);
            }
            catch (Exception ex) when ((ex is HttpRequestException { StatusCode: null } or TaskCanceledException) && cached is not null)
            {
                // Offline or backend not running: keep showing the last page we had instead of an empty screen.
                _logger.LogWarning(ex, "API unreachable; showing cached page for {Path}.", path);
                return CloneRooms(cached!.Rooms);
            }
        }

        private async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response)
        {
            try
            {
                var error = await response.Content.ReadFromJsonAsync(RoomListingJsonContext.Default.ApiErrorDto);
                return string.IsNullOrWhiteSpace(error?.Message) ? null : error.Message;
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                _logger.LogDebug(ex, "Error response without a JSON message.");
                return null;
            }
        }

        /// <summary>Card DTO → domain model. Fields not in the card (landlord contact, rules...) keep their defaults.</summary>
        private static RoomListing ToRoomListing(RoomCardDto card) => new()
        {
            Id = card.Id,
            Title = card.Title ?? string.Empty,
            MonthlyRent = card.MonthlyRent,
            Area = card.Area,
            DistanceToCampusKm = card.DistanceToCampusKm,
            IsVerified = card.IsVerified,
            ThumbnailUrl = string.IsNullOrWhiteSpace(card.ThumbnailUrl) ? "dotnet_bot.png" : card.ThumbnailUrl,
            RoomType = card.RoomType ?? string.Empty,
            Address = card.Address ?? string.Empty,
            Ward = card.Ward ?? string.Empty,
            WalkMinutes = card.WalkMinutes,
            BusMinutes = card.BusMinutes,
            MotorbikeMinutes = card.MotorbikeMinutes,
            Status = Constants.ListingStatuses.Available, // the API only returns Available rooms (AC4)
        };

        // Callers may change items (e.g. IsFavorite), so never hand out the cached instances.
        private static List<RoomListing> CloneRooms(IEnumerable<RoomListing> rooms) => rooms.Select(Clone).ToList();

        private static RoomListing Clone(RoomListing room) => new()
        {
            Id = room.Id,
            Title = room.Title,
            MonthlyRent = room.MonthlyRent,
            Area = room.Area,
            DistanceToCampusKm = room.DistanceToCampusKm,
            IsVerified = room.IsVerified,
            ThumbnailUrl = room.ThumbnailUrl,
            RoomType = room.RoomType,
            Address = room.Address,
            Ward = room.Ward,
            WalkMinutes = room.WalkMinutes,
            BusMinutes = room.BusMinutes,
            MotorbikeMinutes = room.MotorbikeMinutes,
            Status = room.Status,
            IsFavorite = room.IsFavorite,
        };

        private sealed record CachedPage(EntityTagHeaderValue ETag, IReadOnlyList<RoomListing> Rooms);
    }

    // ---- JSON contract of RoomListingController (backend/RoomToRentRadar.Api/Models/RoomListingModels.cs) ----

    internal sealed record RoomCardDto(
        int Id, string? Title, decimal MonthlyRent, double Area, double DistanceToCampusKm, bool IsVerified,
        string? ThumbnailUrl, string? RoomType, string? Address, string? Ward,
        int WalkMinutes, int BusMinutes, int MotorbikeMinutes);

    internal sealed record RoomListingPageDto(int PageIndex, int PageSize, string? SortBy, bool HasMore, List<RoomCardDto> Items);

    internal sealed record ApiErrorDto(string? Message);

    /// <summary>Source-generated JSON (camelCase like the API): no reflection, safe with Android trimming.</summary>
    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
    [JsonSerializable(typeof(RoomListingPageDto))]
    [JsonSerializable(typeof(ApiErrorDto))]
    internal sealed partial class RoomListingJsonContext : JsonSerializerContext
    {
    }
}
