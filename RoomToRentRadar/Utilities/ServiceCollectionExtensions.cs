namespace RoomToRentRadar
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTransientWithShellRoute<TPage, TModel>(
            this IServiceCollection services,
            string route)
            where TPage : ContentPage
            where TModel : class
        {
            services.AddTransient<TPage>();
            services.AddTransient<TModel>();
            Routing.RegisterRoute(route, typeof(TPage));
            return services;
        }
    }
}

