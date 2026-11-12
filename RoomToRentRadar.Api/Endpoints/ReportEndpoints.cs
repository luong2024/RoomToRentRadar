namespace RoomToRentRadar.Api.Endpoints
{
    /// <summary>
    /// TASK OWNER: T13.1 (Lâm Minh Hiếu — US13: Build listing report API and moderation queue entry).
    /// Only T13.1 edits this file. Do NOT edit Program.cs.
    /// </summary>
    public static class ReportEndpoints
    {
        public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/reports");

            // TODO [T13.1 - Hieu]: Map POST /api/reports and GET /api/reports/pending using IListingReportRepository

            return app;
        }
    }
}
