using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Data;
// "Models" alone resolves to RoomToRentRadar.Api.Models inside this project, so alias the domain type.
using RoomListing = RoomToRentRadar.Models.RoomListing;

namespace RoomToRentRadar.Api.Controllers
{
    /// <summary>
    /// US02 / T02.1 — "C" of the MVC Web API: GET /api/rooms, a page of Available + Verified rooms.
    /// Public (guests can browse). Sorting, filtering and paging happen in SQL (RoomListingRepository.Listing.cs):
    /// AC1 default sort by distance to ICTU, AC4 only Available + Verified, AC3 fast for 500 listings.
    /// Caching (US02 "pagination and caching"): HTTP revalidation with a strong ETag. The client keeps the
    /// response and sends If-None-Match; when nothing changed the API answers 304 with no body, so data is
    /// never stale (Cache-Control: no-cache = "always ask the server first").
    /// </summary>
    [ApiController]
    [Route("api/rooms")]
    [Produces("application/json")]
    public sealed class RoomListingController : ControllerBase
    {
        private const int MaxPageSize = 100;

        private static readonly string[] SupportedSorts =
        [
            RoomListingRepository.SortDistanceAsc,
            RoomListingRepository.SortPriceAsc,
            RoomListingRepository.SortPriceDesc,
        ];

        private readonly IRoomListingRepository _rooms;
        private readonly ILogger<RoomListingController> _logger;

        public RoomListingController(IRoomListingRepository rooms, ILogger<RoomListingController> logger)
        {
            _rooms = rooms;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/rooms?pageIndex=0&amp;pageSize=20&amp;sortBy=DistanceAsc
        /// → 200 page | 304 not modified (If-None-Match matches) | 400 invalid query.
        /// </summary>
        [HttpGet]
        [ProducesResponseType<RoomListingPage>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> List(
            [FromQuery] int pageIndex = 0,
            [FromQuery] int pageSize = 20,
            [FromQuery] string sortBy = RoomListingRepository.SortDistanceAsc)
        {
            // The repository silently clamps bad values; the HTTP API reports them so clients notice mistakes.
            if (pageIndex < 0)
            {
                return BadRequest(new ErrorResponse("pageIndex phải lớn hơn hoặc bằng 0."));
            }

            if (pageSize is < 1 or > MaxPageSize)
            {
                return BadRequest(new ErrorResponse($"pageSize phải từ 1 đến {MaxPageSize}."));
            }

            var sort = SupportedSorts.FirstOrDefault(s => string.Equals(s, sortBy?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (sort is null)
            {
                return BadRequest(new ErrorResponse($"sortBy không hợp lệ. Chỉ hỗ trợ: {string.Join(", ", SupportedSorts)}."));
            }

            var rooms = await _rooms.ListVerifiedRoomsAsync(pageIndex, pageSize, sort);
            var page = new RoomListingPage(
                pageIndex,
                pageSize,
                sort,
                HasMore: rooms.Count == pageSize,
                Items: rooms.Select(ToCard).ToList());

            // ETag = hash of the exact content; any change in price, status, order or paging changes it.
            var etag = new EntityTagHeaderValue(ComputeETag(page));
            Response.Headers.ETag = etag.ToString();
            Response.Headers.CacheControl = "no-cache";

            if (ClientHasCurrentVersion(etag))
            {
                _logger.LogDebug("Room listing page {PageIndex} unchanged; returning 304.", pageIndex);
                return StatusCode(StatusCodes.Status304NotModified);
            }

            return Ok(page);
        }

        private bool ClientHasCurrentVersion(EntityTagHeaderValue current)
        {
            var ifNoneMatch = Request.GetTypedHeaders().IfNoneMatch;
            return ifNoneMatch is { Count: > 0 }
                   && ifNoneMatch.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(current, useStrongComparison: true));
        }

        private static string ComputeETag(RoomListingPage page)
        {
            var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(page));
            return $"\"{Convert.ToHexString(hash, 0, 16).ToLowerInvariant()}\"";
        }

        /// <summary>AC2 card fields only; landlord contact stays on the detail screen (US03 AC4).</summary>
        private static RoomCardResponse ToCard(RoomListing room) => new(
            room.Id,
            room.Title,
            room.MonthlyRent,
            room.Area,
            room.DistanceToCampusKm,
            room.IsVerified,
            room.ThumbnailUrl,
            room.RoomType,
            room.Address,
            room.Ward,
            room.WalkMinutes,
            room.BusMinutes,
            room.MotorbikeMinutes);
    }
}
