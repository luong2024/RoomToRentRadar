namespace RoomToRentRadar.Api.Endpoints
{
    /// <summary>
    /// TASK OWNER: T12.1 (Liễu Hải Nam — US12: Build house rules model and API).
    /// Only T12.1 edits this file. Do NOT edit Program.cs.
    /// </summary>
    public static class HouseRuleEndpoints
    {
        public static IEndpointRouteBuilder MapHouseRuleEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/house-rules");

            // TODO [T12.1 - Nam]: Map GET /api/house-rules/{roomId:int} and POST /api/house-rules using IHouseRuleRepository

            return app;
        }
    }
}
