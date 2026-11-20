using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Data;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Api.Endpoints
{
    /// <summary>
    /// TASK OWNER: T02.1 (Phạm Thị Thùy — US02: Build listing API with pagination and sorting).
    /// Only T02.1 edits this file. Do NOT edit Program.cs.
    /// GET /api/rooms?pageIndex=0&amp;pageSize=20&amp;sortBy=DistanceAsc — public (guests can browse).
    /// </summary>
    public static class RoomListingEndpoints
    {
        private const int MaxPageSize = 100;

        private static readonly string[] SupportedSorts =
        [
            RoomListingRepository.SortDistanceAsc,
            RoomListingRepository.SortPriceAsc,
            RoomListingRepository.SortPriceDesc,
        ];

        public static IEndpointRouteBuilder MapRoomListingEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/rooms");

            // GET /api/rooms → 200 page of Available + Verified rooms | 400 invalid query
            group.MapGet("/", async (IRoomListingRepository rooms, int pageIndex = 0, int pageSize = 20, string sortBy = "DistanceAsc") =>
            {
                // The repository silently clamps bad values; the HTTP API reports them so clients notice mistakes.
                if (pageIndex < 0)
                {
                    return Results.BadRequest(new RoomListingError("pageIndex phải lớn hơn hoặc bằng 0."));
                }

                if (pageSize < 1 || pageSize > MaxPageSize)
                {
                    return Results.BadRequest(new RoomListingError($"pageSize phải từ 1 đến {MaxPageSize}."));
                }

                var sort = SupportedSorts.FirstOrDefault(s => string.Equals(s, sortBy?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (sort is null)
                {
                    return Results.BadRequest(new RoomListingError(
                        $"sortBy không hợp lệ. Chỉ hỗ trợ: {string.Join(", ", SupportedSorts)}."));
                }

                var items = await rooms.ListVerifiedRoomsAsync(pageIndex, pageSize, sort);

                // No total count in the contract: a full page means the client may request the next one.
                return Results.Ok(new RoomListingPage(
                    pageIndex,
                    pageSize,
                    sort,
                    HasMore: items.Count == pageSize,
                    Items: items.Select(ToCard).ToList()));
            })
            .WithName("ListVerifiedRooms");

            return app;
        }

        /// <summary>
        /// AC2 card fields only. Landlord phone/name are deliberately left out: the listing is public,
        /// and contact details belong to the room detail screen (US03 guest masking).
        /// </summary>
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

    /// <summary>One room card on the listing screen (US02 AC2).</summary>
    public sealed record RoomCardResponse(
        int Id,
        string Title,
        decimal MonthlyRent,
        double Area,
        double DistanceToCampusKm,
        bool IsVerified,
        string ThumbnailUrl,
        string RoomType,
        string Address,
        string Ward,
        int WalkMinutes,
        int BusMinutes,
        int MotorbikeMinutes);

    /// <summary>A page of room cards plus the paging parameters actually used.</summary>
    public sealed record RoomListingPage(int PageIndex, int PageSize, string SortBy, bool HasMore, List<RoomCardResponse> Items);

    /// <summary>Error body with a Vietnamese message.</summary>
    public sealed record RoomListingError(string Message);
}
