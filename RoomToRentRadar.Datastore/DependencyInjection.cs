using Microsoft.Extensions.DependencyInjection;

namespace RoomToRentRadar.Datastore;

public static class DependencyInjection
{
    public static IServiceCollection AddDatastore(this IServiceCollection services)
    {
        // Register CosmosDB contexts and SQLite connection factories here
        return services;
    }
}

