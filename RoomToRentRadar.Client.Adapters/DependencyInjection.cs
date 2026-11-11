using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;

namespace RoomToRentRadar.Client.Adapters;

public static class DependencyInjection
{
    public static IServiceCollection AddClientAuthGateway(
        this IServiceCollection services,
        Uri baseAddress,
        TimeSpan? timeout = null)
    {
        services.AddHttpClient<IAuthGateway, HttpAuthGateway>(client =>
        {
            client.BaseAddress = baseAddress;
            client.Timeout = timeout ?? TimeSpan.FromSeconds(15);
        });

        return services;
    }
}

