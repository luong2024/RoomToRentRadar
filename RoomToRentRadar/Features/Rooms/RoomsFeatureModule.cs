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
            // T02.1/T03.1/T05.1/T09.2: services.AddSingleton<IRoomListingRepository, RoomListingRepository>();
            // T02.2 (Hieu):
            services.AddTransientWithShellRoute<RoomListPage, RoomListPageModel>(Constants.Routes.RoomList);
            // T03.2 (Nam):   services.AddTransientWithShellRoute<RoomDetailPage, RoomDetailPageModel>(Constants.Routes.RoomDetail);
            // T04.2 (Hieu):  services.AddTransientWithShellRoute<PhotoGallery360Page, PhotoGallery360PageModel>(Constants.Routes.RoomGallery360);
            // T05.2 (Nam):   services.AddTransientWithShellRoute<RoomFilterModalPage, RoomFilterPageModel>(Constants.Routes.RoomFilter);
            // T12.1 (Nam):   services.AddSingleton<IHouseRuleRepository, HouseRuleRepository>();
            return services;
        }
    }
}
