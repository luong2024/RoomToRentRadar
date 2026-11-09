using Microsoft.Extensions.DependencyInjection;

namespace RoomToRentRadar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Register Outbound Port Repository & Adapter implementations (I*Repository -> *Repository, I*Port -> *Adapter) here
        return services;
    }
}

