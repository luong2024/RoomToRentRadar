namespace RoomToRentRadar.Features.Favorites
{
    /// <summary>
    /// Self-contained DI &amp; Shell Route registration for US07 Favorites (T07.1 &amp; T07.2).
    /// </summary>
    public static class FavoritesFeatureModule
    {
        public static IServiceCollection AddFavoritesFeature(this IServiceCollection services)
        {
            // T07.1 (Quang): real SQLite favorites; overrides the InMemoryFavoriteRepository fallback.
            services.AddSingleton<IFavoriteRepository, FavoriteRepository>(); // singleton: shares the duplicate-guard lock
            // T07.2 (Hieu):  services.AddTransientWithShellRoute<FavoritesPage, FavoritesPageModel>(Constants.Routes.Favorites);
            return services;
        }
    }
}
