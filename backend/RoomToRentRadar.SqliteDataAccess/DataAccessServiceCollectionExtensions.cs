using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.SqliteDataAccess.Interceptors;
using RoomToRentRadar.SqliteDataAccess.Repositories;

namespace RoomToRentRadar.SqliteDataAccess;

public static class DataAccessServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccess(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton<SqlitePragmaInterceptor>();
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(serviceProvider.GetRequiredService<SqlitePragmaInterceptor>());
            options.AddInterceptors(serviceProvider.GetServices<DbCommandInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuthSessionRepository, AuthSessionRepository>();
        services.AddScoped<IRegistrationChallengeRepository, RegistrationChallengeRepository>();
        services.AddScoped<IListingLookupRepository, ListingLookupRepository>();
        services.AddScoped<IListingFavouriteRepository, ListingFavouriteRepository>();
        services.AddScoped<IListingPhotoRepository, ListingPhotoRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<ISavedSearchRepository, SavedSearchRepository>();
        services.AddScoped<ISavedSearchAlertLogRepository, SavedSearchAlertLogRepository>();
        services.AddScoped<IListingReportRepository, ListingReportRepository>();
        services.AddScoped<IModerationQueueRepository, ModerationQueueRepository>();
        services.AddScoped<IModerationActionRepository, ModerationActionRepository>();
        services.AddScoped<IListingsQueryRepository, ListingsQueryRepository>();
        services.AddScoped<IListingManagementRepository, ListingManagementRepository>();
        services.AddScoped<IHouseRulesRepository, HouseRulesRepository>();

        return services;
    }
}
