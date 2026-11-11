using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases;

namespace RoomToRentRadar.Bootstrap.DependencyInjection;

public static class RoomDetailServiceRegistration
{
    public static IServiceCollection AddRoomDetailServices(this IServiceCollection services)
    {
        services.AddScoped<IRoomRepository, CosmosRoomRepository>();
        services.AddScoped<IGetRoomDetailsUseCase, GetRoomDetailsUseCase>();

        return services;
    }
}
