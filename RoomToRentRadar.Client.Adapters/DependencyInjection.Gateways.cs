using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;

namespace RoomToRentRadar.Client.Adapters;

public static partial class DependencyInjection
{
    private static void AddFeatureGateways(IServiceCollection services, ApiOptions options)
    {
        AddApiHttpClient<IRoomGateway, HttpRoomGateway>(services, options);
        AddApiHttpClient<ICampusGateway, HttpCampusGateway>(services, options);
    }
}