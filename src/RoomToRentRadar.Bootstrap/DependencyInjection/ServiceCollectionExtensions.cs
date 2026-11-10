using RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;

namespace RoomToRentRadar.Bootstrap.DependencyInjection;

/// <summary>
/// Composition root registrations connecting Application ports to Inbound and Outbound adapters.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationUseCases(this IServiceCollection services)
    {
        // Register Application use cases (Inbound Ports -> UseCase implementations) here.
        return services;
    }

    public static IServiceCollection AddOutboundAdapters(this IServiceCollection services, IConfiguration configuration)
    {
        // Register Outbound Adapters (Cosmos DB repositories, external services) here.
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
