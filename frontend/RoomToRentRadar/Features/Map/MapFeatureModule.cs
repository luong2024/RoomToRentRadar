namespace RoomToRentRadar.Features.Map
{
    /// <summary>
    /// Self-contained DI &amp; Shell Route registration for US06 Interactive Campus Map (T06.1 &amp; T06.2).
    /// </summary>
    public static class MapFeatureModule
    {
        public static IServiceCollection AddMapFeature(this IServiceCollection services)
        {
            // T06.1 (Hieu) & T06.2 (Nam):
            // services.AddTransientWithShellRoute<RoomMapPage, RoomMapPageModel>(Constants.Routes.RoomMap);
            return services;
        }
    }
}
