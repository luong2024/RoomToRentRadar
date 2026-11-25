namespace RoomToRentRadar.Api.Endpoints
{
    /// <summary>
    /// TASK OWNER: T09.2 (Lâm Minh Hiếu — US09: Build listing create/edit API with pending review status).
    /// Only T09.2 edits this file. Do NOT edit Program.cs.
    /// </summary>
    public static class RoomPublishEndpoints
    {
        public static IEndpointRouteBuilder MapRoomPublishEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/rooms");

            // TODO [T09.2 - Hieu]: Map POST /api/rooms and PATCH /api/rooms/{id:int}/status using IRoomListingRepository

            return app;
        }
    }
}
