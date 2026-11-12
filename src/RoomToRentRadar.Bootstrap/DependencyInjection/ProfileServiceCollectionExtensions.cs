using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Profile;

namespace RoomToRentRadar.Bootstrap.DependencyInjection;

public static class ProfileServiceCollectionExtensions
{
    public static IServiceCollection AddProfileFeature(this IServiceCollection services)
    {
        services.AddSingleton<IUserProfileRepository, CosmosUserProfileRepository>();
        services.AddScoped<IGetMyProfileUseCase, GetMyProfileUseCase>();
        services.AddScoped<IUpdateTenantProfileUseCase, UpdateTenantProfileUseCase>();
        services.AddScoped<IUpdateLandlordProfileUseCase, UpdateLandlordProfileUseCase>();
        services.AddScoped<IVerifyLandlordKycUseCase, VerifyLandlordKycUseCase>();
        services.AddScoped<IGetPublicProfileUseCase, GetPublicProfileUseCase>();

        return services;
    }
}