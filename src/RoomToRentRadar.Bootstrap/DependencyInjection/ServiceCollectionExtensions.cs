using RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.UseCases.RoomListings;

namespace RoomToRentRadar.Bootstrap.DependencyInjection;

/// <summary>
/// Composition root registrations connecting Application ports to Inbound and Outbound adapters.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationUseCases(this IServiceCollection services)
    {
        services.AddScoped<IGetRoomListingsUseCase, GetRoomListingsUseCase>();
        return services;
    }

    public static IServiceCollection AddOutboundAdapters(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCachingAdapters();
        services.AddCosmosDbPersistence(configuration);
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
