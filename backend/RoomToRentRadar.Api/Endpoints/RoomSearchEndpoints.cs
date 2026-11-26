namespace RoomToRentRadar.Api.Endpoints
{
    /// <summary>
    /// TASK OWNER: T05.1 (Phạm Thị Thùy — US05: Build search API with price, area, and distance filters).
    /// Only T05.1 edits this file. Do NOT edit Program.cs.
    /// </summary>
    public static class RoomSearchEndpoints
    {
        public static IEndpointRouteBuilder MapRoomSearchEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/rooms");

            // TODO [T05.1 - Thuy]: Map POST /api/rooms/search (RoomFilterCriteria) using IRoomListingRepository

            return app;
        }
    }
}
