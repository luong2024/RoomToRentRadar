using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.UseCases.Auth;
using RoomToRentRadar.UseCases.Favourites;
using RoomToRentRadar.UseCases.Listings;
using RoomToRentRadar.UseCases.Moderation;
using RoomToRentRadar.UseCases.Notifications;
using RoomToRentRadar.UseCases.Photos;
using RoomToRentRadar.UseCases.Reports;
using RoomToRentRadar.UseCases.SavedSearches;
using RoomToRentRadar.UseCases.Travel;

namespace RoomToRentRadar.UseCases;

public static class UseCaseServiceCollectionExtensions
{
    public static IServiceCollection AddUseCases(this IServiceCollection services)
    {
        services.AddScoped<IAuthUseCase, AuthService>();
        services.AddScoped<IFavouritesUseCase, FavouritesService>();
        services.AddScoped<INotificationsUseCase, NotificationsService>();
        services.AddScoped<ISavedSearchesUseCase, SavedSearchService>();
        services.AddScoped<ISavedSearchAlertUseCase, SavedSearchAlertEvaluator>();
        services.AddScoped<IListingReportsUseCase, ListingReportsService>();
        services.AddScoped<ITravelUseCase, TravelEstimatesService>();
        services.AddScoped<IListingPhotosUseCase, ListingPhotosService>();
        services.AddScoped<IModerationQueueUseCase, ModerationQueueService>();
        services.AddScoped<IModerationActionsUseCase, ModerationActionsService>();
        services.AddScoped<IListingsUseCase, ListingsQueryService>();
        services.AddScoped<IAreaSearchUseCase, AreaSearchService>();
        services.AddScoped<IListingManagementUseCase, ListingManagementService>();
        services.AddScoped<IHouseRulesUseCase, HouseRulesService>();
        services.AddScoped<ListingCandidatePageBuilder>();

        return services;
    }
}
