using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Datastore.CosmosDb.Configurations;
using RoomToRentRadar.Datastore.CosmosDb.Context;
using RoomToRentRadar.Datastore.CosmosDb.Repositories;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.CosmosDb;

public static class DependencyInjection
{
    public static IServiceCollection AddCosmosDatastore(
        this IServiceCollection services,
        CosmosDbSettings? settings = null)
    {
        var effectiveSettings = settings ?? new CosmosDbSettings();
        services.AddSingleton(effectiveSettings);
        services.AddSingleton<CosmosDbContext>();
        services.AddScoped<IUserRepository, CosmosUserRepository>();
        services.AddScoped<IAuditLogRepository, CosmosAuditLogRepository>();

        return services;
    }
}

