using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Features.Home.ViewModels;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadar.Infrastructure.DependencyInjection;

/// <summary>
/// Platform-agnostic DI registrations for core services and ViewModels that can be
/// validated on headless CI runners without initializing platform-specific MAUI handlers.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddRoomToRentRadarCore(this IServiceCollection services)
    {
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IApiClient, ApiClient>();
        return services;
    }

    public static IServiceCollection AddRoomToRentRadarViewModels(this IServiceCollection services)
    {
        services.AddTransient<HomeViewModel>();
        return services;
    }
}
