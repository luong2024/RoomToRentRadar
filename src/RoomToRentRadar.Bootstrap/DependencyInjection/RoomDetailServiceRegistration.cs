using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
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

        if (string.Equals(endpoint, "in-memory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<InMemoryRoomRepository>();
            services.AddScoped<IRoomRepository>(sp => sp.GetRequiredService<InMemoryRoomRepository>());
            services.TryAddScoped<IGetRoomDetailsUseCase, GetRoomDetailsUseCase>();
            services.TryAddScoped<ISearchRoomsUseCase, SearchRoomsUseCase>();
            return services;
        }

        // Reuse CosmosDbContext's Rooms container and CosmosClient instead of creating duplicate instances
        services.AddScoped<Container>(sp =>
        {
            var dbContext = sp.GetRequiredService<ICosmosDbContext>();
            return dbContext.RoomsContainer
                ?? throw new InvalidOperationException("Cosmos DB RoomsContainer is not available.");
        });

        services.AddScoped<CosmosClient>(sp =>
        {
            var dbContext = sp.GetRequiredService<ICosmosDbContext>() as CosmosDbContext;
            return dbContext?.Client
                ?? throw new InvalidOperationException("Cosmos DB Client is not available.");
        });

        services.AddScoped<IRoomRepository, CosmosRoomRepository>();
        services.TryAddScoped<IGetRoomDetailsUseCase, GetRoomDetailsUseCase>();
        services.TryAddScoped<ISearchRoomsUseCase, SearchRoomsUseCase>();

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
