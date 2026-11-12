namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Adapters.Outbound.Caching;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.Ports.Outbound;

public static class CosmosServiceCollectionExtensions
{
    public static IServiceCollection AddCosmosDbPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CosmosDbSettings>(configuration.GetSection(CosmosDbSettings.SectionName));

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<CosmosDbSettings>>().Value;

            CosmosClientOptions options;
            if (settings.IsEmulator)
            {
                options = new CosmosClientOptions
                {
                    HttpClientFactory = () =>
                    {
                        var httpMessageHandler = new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        };
                        return new HttpClient(httpMessageHandler);
                    },
                    ConnectionMode = ConnectionMode.Gateway
                };
            }
            else
            {
                options = new CosmosClientOptions();
            }

            return new CosmosClient(settings.AccountEndpoint, settings.AccountKey, options);
        });

        services.AddScoped<IRoomListingRepository, CosmosRoomListingRepository>();
        services.AddScoped<ICampusRepository, CosmosCampusRepository>();
        services.AddScoped<CosmosDbInitializer>();

        return services;
    }

    public static IServiceCollection AddCachingAdapters(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();
        return services;
    }
}
