namespace RoomToRentRadar.Features.Auth
{
    /// <summary>
    /// Self-contained DI &amp; Shell Route registration for US01 Authentication (T01.1 &amp; T01.2).
    /// Edit this file instead of MauiProgram.cs to avoid merge conflicts.
    /// </summary>
    public static class AuthFeatureModule
    {
        public static IServiceCollection AddAuthFeature(this IServiceCollection services)
        {
            // T01.1 (Quang): real SQLite auth; overrides the InMemoryAuthService fallback.
            services.AddSingleton<UserRepository>();
            services.AddSingleton<IAuthService, AuthService>(); // singleton: holds the signed-in session
            // T01.2 (Nam):   services.AddTransientWithShellRoute<LoginPage, AuthPageModel>(Constants.Routes.Login);
            //                services.AddTransientWithShellRoute<RegisterPage, AuthPageModel>(Constants.Routes.Register);
            return services;
        }
    }
}
