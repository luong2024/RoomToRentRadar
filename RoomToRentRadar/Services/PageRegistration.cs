using RoomToRentRadar.PageModels;
using RoomToRentRadar.Pages;

namespace RoomToRentRadar.Services;

/// <summary>
/// Explicit page + page-model DI registration (T00.15); no reflection scanning so registrations
/// stay visible and trim-safe.
/// </summary>
public static class PageRegistration
{
    public static IServiceCollection AddPage<TPage, TModel>(this IServiceCollection services)
        where TPage : class
        where TModel : class
    {
        services.AddTransient<TPage>();
        services.AddTransient<TModel>();
        return services;
    }

    public static IServiceCollection AddAppPages(this IServiceCollection services)
    {
        services.AddPage<MainLandingPage, MainLandingPageModel>();
        services.AddPage<AccountPlaceholderPage, AccountPageModel>();
        services.AddTransient<MapPlaceholderPage>();
        services.AddPage<SavedPlaceholderPage, SavedPageModel>();
        services.AddPage<MessagesPlaceholderPage, MessagesPageModel>();

        services.AddPage<LoginPage, LoginPageModel>();
        services.AddPage<RegisterPage, RegisterPageModel>();
        services.AddPage<RoleSelectionPage, RoleSelectionPageModel>();

        // One shared placeholder for the 12 detail routes and the my-listings / moderation tabs (T00.15).
        services.AddPage<PlaceholderPage, PlaceholderPageModel>();

        // Room Detail Screen (US02 / T02.2)
        services.AddPage<RoomDetailPage, RoomDetailPageModel>();

        services.AddSingleton<UiCatalogPageModel>();
        services.AddTransient<UiCatalogPage>();

        return services;
    }
}