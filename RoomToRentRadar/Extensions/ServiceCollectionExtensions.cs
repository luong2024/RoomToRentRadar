using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using RoomToRentRadar.Constants;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;
using RoomToRentRadar.ViewModels;
using RoomToRentRadar.Views;
using RoomToRentRadar.Services.Api;

namespace RoomToRentRadar.Extensions;

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
        services.AddSingleton<IAlertPrompt, ShellAlertPrompt>();
        services.AddSingleton<IDeviceLocationService, MauiDeviceLocationService>();
        services.AddSingleton<IUndoService, UndoService>();
        services.AddSingleton<AuthService>();
        services.AddSingleton<IListingManagementClient>(provider => new ListingManagementClient(new HttpClient(
            provider.GetRequiredService<AuthenticatedHttpMessageHandler>())
        {
            BaseAddress = new Uri(ApiConstants.BaseAddress),
            Timeout = TimeSpan.FromMinutes(3)
        }));
        services.AddSingleton<IListingReportsClient>(serviceProvider =>
        {
            var handler = serviceProvider.GetRequiredService<AuthenticatedHttpMessageHandler>();
            return new ListingReportsClient(new HttpClient(handler)
            {
                BaseAddress = new Uri(ApiConstants.BaseAddress),
                Timeout = TimeSpan.FromSeconds(15)
            });
        });
        services.AddSingleton<IModerationClient>(serviceProvider =>
        {
            var handler = serviceProvider.GetRequiredService<AuthenticatedHttpMessageHandler>();
            return new ModerationClient(new HttpClient(handler)
            {
                BaseAddress = new Uri(ApiConstants.BaseAddress),
                Timeout = TimeSpan.FromSeconds(15)
            });
        });
        services.AddSingleton<IErrorHandler, ModalErrorHandler>();
        services.AddSingleton<IPreferences>(Preferences.Default);
        services.AddSingleton<ICampusSelectionStore, PreferencesCampusSelectionStore>();
        services.AddSingleton<IRoomComparisonStore, RoomComparisonStore>();
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
        services.AddTransient<RoomReportViewModel>();
        services.AddTransient<RoomDetailPage>();
        services.AddTransient<FavouritesViewModel>();
        services.AddTransient<FavouritesPage>();
        services.AddTransient<DirectionsPage>();
        services.AddTransient<ComparisonViewModel>();
        services.AddTransient<ComparisonPage>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<LoginPage>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<RegisterPage>();
        services.AddTransient<OtpVerificationViewModel>();
        services.AddTransient<OtpVerificationPage>();
        services.AddTransient<MyListingsViewModel>();
        services.AddTransient<MyListingsPage>();
        services.AddTransient<ModerationQueueViewModel>();
        services.AddTransient<ModerationQueuePage>();
        services.AddTransient<ListingEditorViewModel>();
        services.AddTransient<ListingEditorPage>();
        services.AddTransient<MapSearchViewModel>();
        services.AddTransient<MapSearchPage>();
        return services;
    }
}
