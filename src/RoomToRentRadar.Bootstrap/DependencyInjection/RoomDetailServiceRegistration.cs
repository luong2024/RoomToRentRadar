using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Adapters.Outbound.Persistence.InMemory;
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
        var cosmosOptions = configuration.GetSection(CosmosDbOptions.SectionName).Get<CosmosDbOptions>() ?? new CosmosDbOptions();
        var endpoint = !string.IsNullOrWhiteSpace(cosmosOptions.AccountEndpoint)
            ? cosmosOptions.AccountEndpoint
            : configuration["CosmosDb:AccountEndpoint"];
        var authKey = !string.IsNullOrWhiteSpace(cosmosOptions.AuthKey)
            ? cosmosOptions.AuthKey
            : (cosmosOptions.AuthKeyOrResourceToken ?? configuration["CosmosDb:AuthKey"] ?? configuration["CosmosDb:AuthKeyOrResourceToken"]);
        var databaseName = !string.IsNullOrWhiteSpace(cosmosOptions.DatabaseName)
            ? cosmosOptions.DatabaseName
            : (configuration["CosmosDb:DatabaseName"] ?? "RoomToRentRadarDb");
        var containerName = !string.IsNullOrWhiteSpace(cosmosOptions.ContainerName)
            ? cosmosOptions.ContainerName
            : (configuration["CosmosDb:ContainerName"] ?? "Rooms");

        if (string.Equals(endpoint, "in-memory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<InMemoryRoomRepository>();
            services.AddScoped<IRoomRepository>(sp => sp.GetRequiredService<InMemoryRoomRepository>());
            services.AddScoped<IGetRoomDetailsUseCase, GetRoomDetailsUseCase>();
            services.AddScoped<ISearchRoomsUseCase, SearchRoomsUseCase>();
            return services;
        }

        if (string.IsNullOrWhiteSpace(endpoint) ||
            string.IsNullOrWhiteSpace(authKey) ||
            string.IsNullOrWhiteSpace(databaseName) ||
            string.IsNullOrWhiteSpace(containerName))
        {
            throw new InvalidOperationException("Thiếu cấu hình Cosmos DB (AccountEndpoint, AuthKey, DatabaseName, hoặc ContainerName).");
        }

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
        services.AddScoped<IRoomRepository, CosmosRoomRepository>();
        services.AddScoped<IGetRoomDetailsUseCase, GetRoomDetailsUseCase>();
        services.AddScoped<ISearchRoomsUseCase, SearchRoomsUseCase>();

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
