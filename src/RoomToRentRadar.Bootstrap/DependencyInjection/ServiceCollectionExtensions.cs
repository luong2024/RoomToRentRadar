using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases;

namespace RoomToRentRadar.Bootstrap.DependencyInjection;

/// <summary>
/// Composition root registrations connecting Application ports to Inbound and Outbound adapters.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationUseCases(this IServiceCollection services)
    {
        services.AddScoped<IGetRoomDetailsUseCase, GetRoomDetailsUseCase>();
        return services;
    }

    public static IServiceCollection AddOutboundAdapters(this IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration["CosmosDb:AccountEndpoint"];
        var authKey = configuration["CosmosDb:AuthKey"];
        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "RoomToRentRadarDb";
        var containerName = configuration["CosmosDb:ContainerName"] ?? "Rooms";

        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(authKey))
        {
            var cosmosClient = new CosmosClient(endpoint, authKey, new CosmosClientOptions
            {
                HttpClientFactory = () => new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                }),
                ConnectionMode = ConnectionMode.Gateway
            });
            var container = cosmosClient.GetContainer(databaseName, containerName);

            services.AddSingleton(cosmosClient);
            services.AddSingleton(container);
            services.AddScoped<IRoomRepository, CosmosRoomRepository>();
        }

        return services;
    }

    public static IServiceCollection AddInboundAdapters(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services
            .AddControllers()
            .AddApplicationPart(typeof(HealthController).Assembly);

        return services;
    }
}
