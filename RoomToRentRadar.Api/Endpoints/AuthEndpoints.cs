namespace RoomToRentRadar.Api.Endpoints
{
    /// <summary>
    /// TASK OWNER: T01.1 (Nguyễn Đăng Quang — US01: Build ASP.NET Core Identity &amp; JWT Token API).
    /// Only T01.1 edits this file. Do NOT edit Program.cs.
    /// </summary>
    public static class AuthEndpoints
    {
        public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/auth");

            // TODO [T01.1 - Quang]: Map POST /api/auth/login and POST /api/auth/register using IAuthService

            return app;
        }
    }
}
