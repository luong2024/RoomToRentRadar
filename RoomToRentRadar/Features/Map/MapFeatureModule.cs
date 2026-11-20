using Microsoft.Maui.Controls;

namespace RoomToRentRadar.Features.Map
{
    /// <summary>
    /// Self-contained DI &amp; Shell Route registration for US06 Interactive Campus Map (T06.1 &amp; T06.2).
    /// </summary>
    public static class MapFeatureModule
    {
        public static IServiceCollection AddMapFeature(this IServiceCollection services)
        {
            services.AddSingleton<ICampusMapService, CampusMapService>();
            services.AddTransient<RoomMapPageModel>();
            services.AddTransient<RoomMapPage>();
            Routing.RegisterRoute(Constants.Routes.RoomMap, typeof(RoomMapPage));
            return services;
        }
    }
}
