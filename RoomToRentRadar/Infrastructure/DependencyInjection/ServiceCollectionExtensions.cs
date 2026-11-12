using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.Home.ViewModels;
using RoomToRentRadar.Features.Home.Views;
using RoomToRentRadar.Features.Login.ViewModels;
using RoomToRentRadar.Features.Login.Views;
using RoomToRentRadar.Features.RoomDetail.ViewModels;
using RoomToRentRadar.Features.RoomDetail.Views;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Infrastructure.Auth;
using RoomToRentRadar.Infrastructure.ErrorHandling;
using RoomToRentRadar.Infrastructure.Navigation;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMobileInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ITokenStore, SecureTokenStore>();
        services.AddTransient<AuthenticatedHttpMessageHandler>();
        services.AddSingleton<IApiClient>(serviceProvider =>
        {
            var handler = serviceProvider.GetRequiredService<AuthenticatedHttpMessageHandler>();
            return new ApiClient(new HttpClient(handler)
            {
                BaseAddress = new Uri(ApiConstants.BaseAddress),
                Timeout = TimeSpan.FromSeconds(15)
            });
        });
        services.AddSingleton<AuthService>();
        services.AddSingleton<IErrorHandler, ModalErrorHandler>();
        services.AddSingleton<IPreferences>(Preferences.Default);
        services.AddSingleton<ISavedAreaSearchStore, PreferencesSavedAreaSearchStore>();
        services.AddSingleton<INavigationService, ShellNavigationService>();
        return services;
    }

    public static IServiceCollection AddMobileFeatures(this IServiceCollection services)
    {
        services.AddSingleton<AppShell>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<HomePage>();
        services.AddTransient<RoomDetailViewModel>();
        services.AddTransient<RoomDetailPage>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<LoginPage>();
        return services;
    }
}
