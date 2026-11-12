namespace RoomToRentRadar.Api.Endpoints
{
    /// <summary>
    /// TASK OWNER: T02.1 (Phạm Thị Thùy — US02: Build listing API with pagination and sorting).
    /// Only T02.1 edits this file. Do NOT edit Program.cs.
    /// </summary>
    public static class RoomListingEndpoints
    {
        public static IEndpointRouteBuilder MapRoomListingEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/rooms");

            // TODO [T02.1 - Thuy]: Map GET /api/rooms (pageIndex, pageSize, sortBy) using IRoomListingRepository

            return app;
        }
    }
}
