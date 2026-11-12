namespace RoomToRentRadar.Api.Endpoints
{
    /// <summary>
    /// TASK OWNER: T04.1 (Nguyễn Đăng Quang — US04: Build photo upload pipeline and gallery API).
    /// Only T04.1 edits this file. Do NOT edit Program.cs.
    /// </summary>
    public static class PhotoGalleryEndpoints
    {
        public static IEndpointRouteBuilder MapPhotoGalleryEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/rooms/{roomId:int}/photos");

            // TODO [T04.1 - Quang]: Map GET/POST room photo gallery & 360° panorama endpoints

            return app;
        }
    }
}
