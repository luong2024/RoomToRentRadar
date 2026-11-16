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
        services.Configure<CosmosDbOptions>(configuration.GetSection(CosmosDbOptions.SectionName));

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
            var authKey = !string.IsNullOrWhiteSpace(settings.AuthKey)
                ? settings.AuthKey
                : settings.AuthKeyOrResourceToken;
            if (string.IsNullOrWhiteSpace(settings.AccountEndpoint) || string.IsNullOrWhiteSpace(authKey))
            {
                throw new InvalidOperationException("CosmosDb:AccountEndpoint and CosmosDb:AuthKey (or AuthKeyOrResourceToken) are required.");
            }

            var options = new CosmosClientOptions
            {
                ConnectionMode = string.Equals(settings.ConnectionMode, "Gateway", StringComparison.OrdinalIgnoreCase)
                    ? ConnectionMode.Gateway
                    : ConnectionMode.Direct
            };
            if (settings.IsEmulator)
            {
                var isLocalEndpoint = Uri.TryCreate(settings.AccountEndpoint, UriKind.Absolute, out var endpointUri) &&
                                      (endpointUri.IsLoopback ||
                                       string.Equals(endpointUri.Host, "localhost", StringComparison.OrdinalIgnoreCase));

                if (!isLocalEndpoint)
                {
                    throw new InvalidOperationException(
                        $"Cosmos DB 'IsEmulator' configuration is strictly allowed only for local loopback endpoints (e.g., localhost). " +
                        $"Configured endpoint '{settings.AccountEndpoint}' is not a local endpoint, and TLS certificate bypass has been blocked.");
                }

                options.HttpClientFactory = () => new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                });
            }

            return new CosmosClient(settings.AccountEndpoint, authKey, options);
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
