using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.ViewModels.Authentication;
using RoomToRentRadar.Views.Authentication;
using RoomToRentRadar.ViewModels.Home;
using RoomToRentRadar.Views.Home;
using RoomToRentRadar.Services.RoomDetails;
using RoomToRentRadar.ViewModels.RoomDetails;
using RoomToRentRadar.Views.RoomDetails;
using RoomToRentRadar.Services.RoomListings;
using RoomToRentRadar.ViewModels.RoomListings;
using RoomToRentRadar.Views.RoomListings;
using RoomToRentRadar.Services.Profile;
using RoomToRentRadar.ViewModels.Profile;
using RoomToRentRadar.Views.Profile;
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

        // Base HttpClient for Auth endpoints and general HTTP access
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

        // Route guard service
        services.AddSingleton<IRouteGuardService, RouteGuardService>();

        services.AddSingleton<IGoogleAuthAdapter, GoogleAuthAdapter>();
        services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<IProfileUiThreadDispatcher, MauiProfileUiThreadDispatcher>();
        services.AddSingleton<IRoomDetailService, RoomDetailService>();
        services.AddSingleton<ITravelTimeService, TravelTimeService>();
        services.AddSingleton<IMediaPickerService, MediaPickerService>();
        return services;
    }

    public static IServiceCollection AddMobileFeatures(this IServiceCollection services)
    {
        services.AddTransient<AppShell>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<HomePage>();
        services.AddTransient<AuthViewModel>();
        services.AddTransient<AuthPage>();
        services.AddTransient<IRoomListingsService, RoomListingsService>();
        services.AddTransient<IFavouritesService, FavouritesService>();
        services.AddTransient<IFavoriteFeedbackService, FavoriteFeedbackService>();
        services.AddTransient<RoomListingsViewModel>();
        services.AddTransient<RoomListingsPage>();
        services.AddTransient<RoomDetailsViewModel>();
        services.AddTransient<RoomDetailsPage>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<ProfilePage>();

        services.AddTransient<RoomDetailViewModel>();
        services.AddTransient<RoomDetailPage>();
        services.AddTransient<RoomToRentRadar.ViewModels.MapSearch.MapSearchViewModel>();
        services.AddTransient<RoomToRentRadar.Views.MapSearch.MapSearchPage>();
        services.AddTransient<ICreateListingService, CreateListingService>();
        services.AddTransient<CreateListingViewModel>();
        services.AddTransient<CreateListingPage>();
        services.AddTransient<ListingLocationPickerViewModel>();
        services.AddTransient<ListingLocationPickerPage>();
        return services;
    }
}
