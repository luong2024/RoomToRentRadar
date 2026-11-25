namespace RoomToRentRadar.Api.Endpoints
{
    /// <summary>
    /// TASK OWNER: T07.1 (Nguyễn Đăng Quang — US07: Build local SQLite favorites table and API sync).
    /// Only T07.1 edits this file. Do NOT edit Program.cs.
    /// </summary>
    public static class FavoriteEndpoints
    {
        public static IEndpointRouteBuilder MapFavoriteEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/favorites");

            // TODO [T07.1 - Quang]: Map GET /api/favorites/{userId:int} and POST /api/favorites/toggle using IFavoriteRepository

            return app;
        }
    }
}
