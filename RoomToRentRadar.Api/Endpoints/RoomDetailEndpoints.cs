namespace RoomToRentRadar.Api.Endpoints
{
    /// <summary>
    /// TASK OWNER: T03.1 (Phạm Thị Thùy — US03: Build room detail API &amp; similar rooms).
    /// Only T03.1 edits this file. Do NOT edit Program.cs.
    /// </summary>
    public static class RoomDetailEndpoints
    {
        public static IEndpointRouteBuilder MapRoomDetailEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/rooms");

            // TODO [T03.1 - Thuy]: Map GET /api/rooms/{id:int} and GET /api/rooms/{id:int}/similar using IRoomListingRepository

            return app;
        }
    }
}
