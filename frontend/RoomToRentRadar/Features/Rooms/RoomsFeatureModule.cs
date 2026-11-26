using CommunityToolkit.Maui;

namespace RoomToRentRadar.Features.Rooms
{
    /// <summary>
    /// Self-contained DI &amp; Shell Route registration for US02, US03, US04, US05, US12 Room screens &amp; services.
    /// Edit this file instead of MauiProgram.cs to avoid merge conflicts.
    /// </summary>
    public static class RoomsFeatureModule
    {
        public static IServiceCollection AddRoomsFeature(this IServiceCollection services)
        {
            // T02.2 (Hieu): the frontend reads rooms from the backend Web API (GET /api/rooms, ETag cache) instead of
            // the local SQLite RoomListingRepository of T02.1, which now runs inside the backend (Docker).
            // Methods not yet implemented by T03.2/T05.2/T09.1 fall back to the in-memory ICTU sample data.
            services.AddSingleton<IRoomListingRepository, RoomToRentRadar.Services.Api.ApiRoomListingRepository>();

            // T02.2 (Hieu): listing screen with room cards and infinite scroll.
            services.AddTransientWithShellRoute<RoomListPage, RoomListPageModel>(Constants.Routes.RoomList);
            // T03.2 (Nam):   services.AddTransientWithShellRoute<RoomDetailPage, RoomDetailPageModel>(Constants.Routes.RoomDetail);
            // T04.2 (Hieu):  services.AddTransientWithShellRoute<PhotoGallery360Page, PhotoGallery360PageModel>(Constants.Routes.RoomGallery360);
            // T05.2 (Nam):   services.AddTransientWithShellRoute<RoomFilterModalPage, RoomFilterPageModel>(Constants.Routes.RoomFilter);
            // T12.1 (Nam):   services.AddSingleton<IHouseRuleRepository, HouseRuleRepository>();
            return services;
        }
    }
}
