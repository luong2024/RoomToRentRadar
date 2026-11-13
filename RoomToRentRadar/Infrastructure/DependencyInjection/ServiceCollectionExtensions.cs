using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.Home.ViewModels;
using RoomToRentRadar.Features.Home.Views;
using RoomToRentRadar.Features.RoomDetails.Services;
using RoomToRentRadar.Features.RoomDetails.ViewModels;
using RoomToRentRadar.Features.RoomDetails.Views;
using RoomToRentRadar.Features.RoomListings.Services;
using RoomToRentRadar.Features.RoomListings.ViewModels;
using RoomToRentRadar.Features.RoomListings.Views;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Services;

namespace RoomToRentRadar.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMobileInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IErrorHandler, ModalErrorHandler>();
        services.AddSingleton(_ => ApiSettings.CreateHttpClient(ApiSettings.BaseUrl));
        services.AddSingleton<IApiClient, ApiClient>();
        services.AddSingleton<IRoomDetailService, RoomDetailService>();
        return services;
    }

    public static IServiceCollection AddMobileFeatures(this IServiceCollection services)
    {
        services.AddTransient<HomeViewModel>();
        services.AddTransient<HomePage>();
        services.AddTransient<IRoomListingsService, RoomListingsService>();
        services.AddTransient<RoomListingsViewModel>();
        services.AddTransient<RoomListingsPage>();
        services.AddTransient<RoomDetailsViewModel>();
        services.AddTransient<RoomDetailsPage>();

        services.AddTransient<RoomDetailViewModel>();
        services.AddTransient<RoomDetailPage>();
        return services;
    }
}
