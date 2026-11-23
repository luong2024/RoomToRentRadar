using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Favourites;

namespace RoomToRentRadar.Bootstrap.DependencyInjection;

public static class FavouriteServiceCollectionExtensions
{
    public static IServiceCollection AddFavouritesFeature(this IServiceCollection services)
    {
        services.AddSingleton<IFavouriteRepository, CosmosFavouriteRepository>();
        services.AddScoped<IGetMyFavouritesUseCase, GetMyFavouritesUseCase>();
        services.AddScoped<IGetMyFavouriteIdsUseCase, GetMyFavouriteIdsUseCase>();
        services.AddScoped<IAddFavouriteUseCase, AddFavouriteUseCase>();
        services.AddScoped<IRemoveFavouriteUseCase, RemoveFavouriteUseCase>();
        services.AddScoped<ISyncFavouritesUseCase, SyncFavouritesUseCase>();

        return services;
    }
}
