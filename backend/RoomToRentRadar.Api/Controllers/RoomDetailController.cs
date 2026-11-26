using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Api.Models;
// "Models" alone resolves to RoomToRentRadar.Api.Models inside this project, so alias the domain type.
using RoomListing = RoomToRentRadar.Models.RoomListing;

namespace RoomToRentRadar.Api.Controllers
{
    /// <summary>
    /// US03 / T03.1 — "C" of the MVC Web API: room detail and similar rooms. Public (guests can view).
    /// AC1: every detail section (description, rent, deposit, area, type, amenities, address, landlord, rules).
    /// AC2: status and last-updated date.
    /// AC3: an unavailable room still answers 200 with a message and similar-room suggestions.
    /// AC4: the landlord phone is masked unless the request carries a valid JWT (signed-in user). Masking is done
    ///      here, not only on the screen, so a guest never receives the real number in the JSON.
    /// Rooms that are not verified (e.g. PendingReview) are not public and answer 404.
    /// </summary>
    [ApiController]
    [Route("api/rooms")]
    [Produces("application/json")]
    [AllowAnonymous]
    public sealed class RoomDetailController : ControllerBase
    {
        private const int DefaultSimilarCount = 3;
        private const int MaxSimilarCount = 20;
        private const string RoomNotFoundMessage = "Không tìm thấy phòng trọ.";
        private const string UnavailableMessage =
            "Phòng này hiện không còn trống. Bạn có thể tham khảo các phòng tương tự bên dưới.";

        private readonly IRoomListingRepository _rooms;
        private readonly ILogger<RoomDetailController> _logger;

        public RoomDetailController(IRoomListingRepository rooms, ILogger<RoomDetailController> logger)
        {
            _rooms = rooms;
            _logger = logger;
        }

        /// <summary>GET /api/rooms/{id} → 200 detail | 400 invalid id | 404 not found or not verified.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType<RoomDetailResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new ErrorResponse("Mã phòng phải lớn hơn 0."));
            }

            var room = await FindPublicRoomAsync(id);
            if (room is null)
            {
                return NotFound(new ErrorResponse(RoomNotFoundMessage));
            }

            var isAvailable = IsAvailable(room);
            IReadOnlyList<RoomCardResponse> similar = isAvailable
                ? []
                : await GetSimilarCardsAsync(room, DefaultSimilarCount);

            return Ok(ToDetail(room, isAvailable, similar, IsSignedIn()));
        }

        /// <summary>
        /// GET /api/rooms/{id}/similar?limit=3 → 200 list | 400 invalid id/limit | 404 room not found.
        /// AC3: Available + Verified rooms in the same ward first, then the closest rent.
        /// </summary>
        [HttpGet("{id:int}/similar")]
        [ProducesResponseType<IReadOnlyList<RoomCardResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSimilar(int id, [FromQuery] int limit = DefaultSimilarCount)
        {
            if (id <= 0)
            {
                return BadRequest(new ErrorResponse("Mã phòng phải lớn hơn 0."));
            }

            if (limit is < 1 or > MaxSimilarCount)
            {
                return BadRequest(new ErrorResponse($"limit phải từ 1 đến {MaxSimilarCount}."));
            }

            var room = await FindPublicRoomAsync(id);
            if (room is null)
            {
                return NotFound(new ErrorResponse(RoomNotFoundMessage));
            }

            return Ok(await GetSimilarCardsAsync(room, limit));
        }

        private async Task<RoomListing?> FindPublicRoomAsync(int id)
        {
            var room = await _rooms.GetByIdAsync(id);
            if (room is { IsVerified: false })
            {
                _logger.LogDebug("Room {RoomId} is not verified; hidden from the public detail API.", id);
                return null;
            }

            return room;
        }

        private async Task<IReadOnlyList<RoomCardResponse>> GetSimilarCardsAsync(RoomListing room, int limit)
        {
            var similar = await _rooms.GetSimilarRoomsAsync(room.Id, room.Ward, room.MonthlyRent, limit);
            return similar.Select(ToCard).ToList();
        }

        /// <summary>AC4: "signed in" = the request carries a valid JWT with a user id (never IAuthService.CurrentUser).</summary>
        private bool IsSignedIn() =>
            User.Identity?.IsAuthenticated == true && AuthController.TryGetUserId(User, out _);

        private static bool IsAvailable(RoomListing room) =>
            string.Equals(room.Status, Constants.ListingStatuses.Available, StringComparison.OrdinalIgnoreCase);

        private static RoomDetailResponse ToDetail(
            RoomListing room, bool isAvailable, IReadOnlyList<RoomCardResponse> similar, bool signedIn) => new(
            room.Id,
            room.Title,
            room.Description,
            room.MonthlyRent,
            room.Deposit,
            room.Area,
            room.RoomType,
            SplitAmenities(room.AmenitiesCsv),
            room.Address,
            room.Ward,
            room.Latitude,
            room.Longitude,
            room.DistanceToCampusKm,
            room.WalkMinutes,
            room.BusMinutes,
            room.MotorbikeMinutes,
            room.ThumbnailUrl,
            room.IsVerified,
            room.Status,
            isAvailable,
            isAvailable ? null : UnavailableMessage,
            room.UpdatedAt,
            new LandlordContactResponse(
                room.LandlordName,
                signedIn ? room.LandlordPhone : room.MaskedLandlordPhone,
                IsPhoneMasked: !signedIn),
            new HouseRulesResponse(
                room.QuietHours,
                room.GuestPolicy,
                room.CookingRules,
                room.ParkingRules,
                room.RulesVersion),
            similar);

        /// <summary>"Wifi, Điều hòa ,,Nóng lạnh" → ["Wifi", "Điều hòa", "Nóng lạnh"].</summary>
        private static IReadOnlyList<string> SplitAmenities(string? csv) =>
            string.IsNullOrWhiteSpace(csv)
                ? []
                : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

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
