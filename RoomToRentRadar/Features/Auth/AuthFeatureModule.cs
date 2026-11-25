using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Services.Api;

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
            // T01.2 (Nam/Quang): the MAUI app is a frontend — auth goes through the backend Web API (T01.1, Docker).
            // Shared HttpClient for ALL feature clients (ApiRoomListingRepository, ApiFavoriteRepository...):
            // base address per platform (ApiEndpoint) + AuthTokenHandler attaching the saved JWT (US01 AC3).
            services.AddSingleton<AuthTokenStore>();
            services.AddSingleton(sp =>
            {
                var handler = new AuthTokenHandler(
                    sp.GetRequiredService<AuthTokenStore>(),
                    sp.GetRequiredService<ILogger<AuthTokenHandler>>())
                {
                    InnerHandler = new HttpClientHandler(),
                };
                return new HttpClient(handler)
                {
                    BaseAddress = ApiEndpoint.BaseAddress,
                    Timeout = TimeSpan.FromSeconds(15),
                };
            });

            // ApiAuthService replaces the local SQLite AuthService of T01.1; one instance serves both contracts.
            services.AddSingleton<ApiAuthService>();
            services.AddSingleton<IAuthService>(sp =>
            {
                var auth = sp.GetRequiredService<ApiAuthService>();
                // Restore the session saved in SecureStorage in the background; LoadAsync logs its own errors.
                _ = auth.RestoreSessionAsync();
                return auth;
            });
            services.AddSingleton<IAuthFeedback>(sp => sp.GetRequiredService<ApiAuthService>());

            // T01.2: Login & Register screens (shared AuthPageModel, one transient instance per page).
            services.AddTransientWithShellRoute<LoginPage, AuthPageModel>(Constants.Routes.Login);
            services.AddTransientWithShellRoute<RegisterPage, AuthPageModel>(Constants.Routes.Register);
            return services;
        }
    }
}
