using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.Home.ViewModels;
using RoomToRentRadar.Features.Home.Views;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Infrastructure.ErrorHandling;

namespace RoomToRentRadar.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMobileInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(_ => new HttpClient
        {
            BaseAddress = new Uri(ApiConstants.BaseAddress),
            Timeout = TimeSpan.FromSeconds(15)
        });
        services.AddSingleton<IApiClient, ApiClient>();
        services.AddSingleton<IErrorHandler, ModalErrorHandler>();
        return services;
    }

    public static IServiceCollection AddMobileFeatures(this IServiceCollection services)
    {
        services.AddSingleton<AppShell>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<HomePage>();
        return services;
    }
}
