using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Datastore.CosmosDb.Configurations;
using RoomToRentRadar.Datastore.CosmosDb.Context;

namespace RoomToRentRadar.Datastore;

public static class DependencyInjection
{
    public static IServiceCollection AddDatastore(
        this IServiceCollection services,
        CosmosDbSettings? settings = null)
    {
        var effectiveSettings = settings ?? new CosmosDbSettings();
        services.AddSingleton(effectiveSettings);
        services.AddSingleton<CosmosDbContext>();

        return services;
    }
}
