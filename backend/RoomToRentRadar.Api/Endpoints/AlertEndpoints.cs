namespace RoomToRentRadar.Api.Endpoints
{
    /// <summary>
    /// TASK OWNER: T08.1 (Phạm Thị Thùy — US08: Build saved-search model and alert criteria API).
    /// Only T08.1 edits this file. Do NOT edit Program.cs.
    /// </summary>
    public static class AlertEndpoints
    {
        public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/alerts");

            // TODO [T08.1 - Thuy]: Map GET/POST/DELETE /api/alerts using ISavedSearchAlertRepository

            return app;
        }
    }
}
