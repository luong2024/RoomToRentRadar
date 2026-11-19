using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.Favourites.ViewModels;
using RoomToRentRadar.Features.Favourites.Views;
using RoomToRentRadar.Features.Home.ViewModels;
using RoomToRentRadar.Features.Home.Views;
using RoomToRentRadar.Features.Login.ViewModels;
using RoomToRentRadar.Features.Login.Views;
using RoomToRentRadar.Features.ListingManagement.ViewModels;
using RoomToRentRadar.Features.ListingManagement.Views;
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
        services.AddSingleton<IFavouritesApiClient>(serviceProvider =>
        {
            var handler = serviceProvider.GetRequiredService<AuthenticatedHttpMessageHandler>();
            return new FavouritesApiClient(new HttpClient(handler)
            {
                BaseAddress = new Uri(ApiConstants.BaseAddress),
                Timeout = TimeSpan.FromSeconds(15)
            });
        });
        services.AddSingleton<IFavouritesService, FavouritesService>();
        services.AddSingleton<IUndoPrompt, SnackbarUndoPrompt>();
        services.AddSingleton<IUndoService, UndoService>();
        services.AddSingleton<AuthService>();
        services.AddSingleton<IListingManagementClient>(provider => new ListingManagementClient(new HttpClient(
            provider.GetRequiredService<AuthenticatedHttpMessageHandler>())
        {
            BaseAddress = new Uri(ApiConstants.BaseAddress),
            Timeout = TimeSpan.FromMinutes(3)
        }));
        services.AddSingleton<IErrorHandler, ModalErrorHandler>();
        services.AddSingleton<IPreferences>(Preferences.Default);
        services.AddSingleton<ICampusSelectionStore, PreferencesCampusSelectionStore>();
        services.AddSingleton<ITravelEstimatesClient>(serviceProvider =>
        {
            var handler = serviceProvider.GetRequiredService<AuthenticatedHttpMessageHandler>();
            return new TravelEstimatesClient(new HttpClient(handler)
            {
                BaseAddress = new Uri(ApiConstants.BaseAddress),
                Timeout = TimeSpan.FromSeconds(15)
            });
        });
        services.AddSingleton<ISavedAreaSearchStore>(provider => new FileSavedAreaSearchStore(
            FileSystem.AppDataDirectory, provider.GetRequiredService<IPreferences>()));
        services.AddSingleton<INavigationService, ShellNavigationService>();
        return services;
    }

    public static IServiceCollection AddMobileFeatures(this IServiceCollection services)
    {
        services.AddSingleton<AppShell>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<HomePage>();
        services.AddTransient<RoomDetailViewModel>();
        services.AddTransient<RoomDetailTravelViewModel>();
        services.AddTransient<RoomDetailPage>();
        services.AddTransient<FavouritesViewModel>();
        services.AddTransient<FavouritesPage>();
        services.AddTransient<DirectionsPage>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<LoginPage>();
        services.AddTransient<MyListingsViewModel>();
        services.AddTransient<MyListingsPage>();
        services.AddTransient<ListingEditorViewModel>();
        services.AddTransient<ListingEditorPage>();
        return services;
    }
}
