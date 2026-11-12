using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases;

namespace RoomToRentRadar.Bootstrap.DependencyInjection;

public static class RoomDetailServiceRegistration
{
    public static IServiceCollection AddRoomDetailServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var endpoint = configuration["CosmosDb:AccountEndpoint"];
        var authKey = configuration["CosmosDb:AuthKey"];
        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "RoomToRentRadarDb";
        var containerName = configuration["CosmosDb:ContainerName"] ?? "Rooms";

        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(authKey))
        {
            CosmosClientOptions clientOptions;
            if (environment.IsDevelopment())
            {
                // bypass TLS
                clientOptions = new CosmosClientOptions
                {
                    HttpClientFactory = () => new HttpClient(new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    }),
                    ConnectionMode = ConnectionMode.Gateway
                };
            }
            else
            {
                // normal validation
                clientOptions = new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway
                };
            }

            var cosmosClient = new CosmosClient(endpoint, authKey, clientOptions);
            var container = cosmosClient.GetContainer(databaseName, containerName);

            services.AddSingleton(cosmosClient);
            services.AddSingleton(container);
        }

        services.AddScoped<IRoomRepository, CosmosRoomRepository>();
        services.AddScoped<IGetRoomDetailsUseCase, GetRoomDetailsUseCase>();

        return services;
    }

    public static IServiceCollection AddRoomDetailServices(
        this IServiceCollection services,
        IWebHostEnvironment environment)
    {
        var serviceProvider = services.BuildServiceProvider();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        return services.AddRoomDetailServices(configuration, environment);
    }
}
