using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.Authentication.ViewModels;
using RoomToRentRadar.Features.Authentication.Views;
using RoomToRentRadar.Features.Home.ViewModels;
using RoomToRentRadar.Features.Home.Views;
using RoomToRentRadar.Infrastructure.Adapters;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Infrastructure.Services;
using RoomToRentRadar.Shared.Services;

namespace RoomToRentRadar.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMobileInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IErrorHandler, ModalErrorHandler>();

        // Secure storage abstraction for token persistence
        services.AddSingleton<ISecureStorage>(SecureStorage.Default);
        services.AddSingleton<ISecureStorageService, MauiSecureStorageService>();

        // DelegatingHandler for bearer token insertion & auto-refresh
        services.AddTransient<AuthenticationHeaderHandler>();

        // Authentication session manager & state provider
        services.AddSingleton<IAuthenticationSessionService, AuthenticationSessionService>();

        // Base HttpClient for Auth endpoints
        services.AddSingleton(sp => new HttpClient
        {
            BaseAddress = ApiConfiguration.GetBaseAddress(),
            Timeout = TimeSpan.FromSeconds(15)
        });
        services.AddSingleton<IAuthenticationPort, HttpAuthAdapter>();

        // ApiClient with AuthenticationHeaderHandler for authorized API requests
        services.AddSingleton<IApiClient>(sp =>
        {
            var handler = sp.GetRequiredService<AuthenticationHeaderHandler>();
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = ApiConfiguration.GetBaseAddress(),
                Timeout = TimeSpan.FromSeconds(15)
            };
            return new ApiClient(httpClient);
        });

        services.AddSingleton<IGoogleAuthAdapter, GoogleAuthAdapter>();
        return services;
    }

    public static IServiceCollection AddMobileFeatures(this IServiceCollection services)
    {
        services.AddTransient<HomeViewModel>();
        services.AddTransient<HomePage>();
        services.AddTransient<AuthViewModel>();
        services.AddTransient<AuthPage>();
        return services;
    }
}
