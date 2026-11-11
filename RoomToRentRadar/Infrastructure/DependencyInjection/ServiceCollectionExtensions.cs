using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.Home.ViewModels;
using RoomToRentRadar.Features.Home.Views;
using RoomToRentRadar.Infrastructure.Adapters;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Services;
using RoomToRentRadar.ViewModels;
using RoomToRentRadar.Views;

namespace RoomToRentRadar.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMobileInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IErrorHandler, ModalErrorHandler>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IApiClient, ApiClient>();
        services.AddSingleton<IGoogleAuthAdapter, GoogleAuthAdapter>();
        services.AddSingleton<IAuthenticationPort, HttpAuthAdapter>();
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
