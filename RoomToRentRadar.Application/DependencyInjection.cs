using Microsoft.Extensions.DependencyInjection;

namespace RoomToRentRadar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register Inbound Port UseCase implementations (I*UseCase -> *UseCase) here
        return services;
    }
}

