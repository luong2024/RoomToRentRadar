using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Client.Adapters.Mocks;

namespace RoomToRentRadar.Client.Adapters;

public static partial class DependencyInjection
{
    private static void AddFeatureGateways(IServiceCollection services, ApiOptions options)
    {
        AddGateway<IRoomGateway, HttpRoomGateway, MockRoomGateway>(services, options);
        AddGateway<ICampusGateway, HttpCampusGateway, MockCampusGateway>(services, options);
        AddApiHttpClient<IFavouriteGateway, HttpFavouriteGateway>(services, options);
        AddApiHttpClient<ISavedSearchGateway, HttpSavedSearchGateway>(services, options);
        AddApiHttpClient<INotificationGateway, HttpNotificationGateway>(services, options);
        AddApiHttpClient<IChatGateway, HttpChatGateway>(services, options);
        AddApiHttpClient<IMyListingGateway, HttpMyListingGateway>(services, options);
        AddApiHttpClient<IReportGateway, HttpReportGateway>(services, options);
        AddApiHttpClient<IAdminGateway, HttpAdminGateway>(services, options);
        AddApiHttpClient<IProfileGateway, HttpProfileGateway>(services, options);
    }

    private static void AddGateway<TService, THttp, TMock>(IServiceCollection services, ApiOptions options)
        where TService : class
        where THttp : class, TService
        where TMock : class, TService
    {
        if (options.UseMocks)
        {
            services.AddSingleton<TService, TMock>();
        }
        else
        {
            AddApiHttpClient<TService, THttp>(services, options);
        }
    }
}