using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RoomToRentRadar.Abstractions.Alerts;
using RoomToRentRadar.Abstractions.Auth;
using RoomToRentRadar.Abstractions.Favorites;
using RoomToRentRadar.Abstractions.HouseRules;
using RoomToRentRadar.Abstractions.Reports;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Data;
using RoomToRentRadar.Infrastructure.Fakes;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Registers core infrastructure and fallback in-memory implementations via <c>TryAddSingleton</c>.
    /// Feature modules run BEFORE or AFTER this: when a developer registers a real SQLite repository
    /// using <c>AddSingleton&lt;IRoomListingRepository, RoomListingRepository&gt;()</c> in their FeatureModule,
    /// it overrides the fallback without touching any other file.
    /// </summary>
    public static class InfrastructureModule
    {
        public static IServiceCollection AddRentalInfrastructure(this IServiceCollection services, string connectionString)
        {
            services.TryAddSingleton<ILogger<RentalDatabaseService>>(NullLogger<RentalDatabaseService>.Instance);
            services.TryAddSingleton(new DatabaseOptions(connectionString));
            services.TryAddSingleton<RentalDatabaseService>();
            services.TryAddSingleton<ComparisonBasketService>();

            // Fallback in-memory contracts (only used when the real feature service is not registered yet)
            services.TryAddSingleton<IAuthService, InMemoryAuthService>();
            services.TryAddSingleton<IRoomListingRepository, InMemoryRoomListingRepository>();
            services.TryAddSingleton<IFavoriteRepository, InMemoryFavoriteRepository>();
            services.TryAddSingleton<ISavedSearchAlertRepository, InMemorySavedSearchAlertRepository>();
            services.TryAddSingleton<IHouseRuleRepository, InMemoryHouseRuleRepository>();
            services.TryAddSingleton<IListingReportRepository, InMemoryListingReportRepository>();

            return services;
        }
    }
}
