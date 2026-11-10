using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.Home.ViewModels;
using RoomToRentRadar.Features.Home.Views;
using RoomToRentRadar.Features.RoomDetails.Services;
using RoomToRentRadar.Features.RoomDetails.ViewModels;
using RoomToRentRadar.Features.RoomDetails.Views;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Services;

namespace RoomToRentRadar.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMobileInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IErrorHandler, ModalErrorHandler>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IApiClient, ApiClient>();
        services.AddSingleton<IRoomDetailService, MockRoomDetailService>();
        return services;
    }

    public static IServiceCollection AddMobileFeatures(this IServiceCollection services)
    {
        services.AddTransient<HomeViewModel>();
        services.AddTransient<HomePage>();
        services.AddTransient<RoomDetailViewModel>();
        services.AddTransient<RoomDetailPage>();
        return services;
    }
}
