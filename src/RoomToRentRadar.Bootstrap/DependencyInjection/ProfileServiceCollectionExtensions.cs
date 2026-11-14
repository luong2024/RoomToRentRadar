using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Adapters.Outbound.Storage;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Profile;

namespace RoomToRentRadar.Bootstrap.DependencyInjection;

public static class ProfileServiceCollectionExtensions
{
    public static IServiceCollection AddProfileFeature(this IServiceCollection services)
    {
        services.TryAddSingleton<IKycUploadRateLimiter, InMemoryKycUploadRateLimiter>();
        services.AddSingleton<IKycDocumentStorageService, KycDocumentStorageService>();
        services.AddScoped<IUserProfileRepository, CosmosUserProfileRepository>();
        services.AddScoped<IGetMyProfileUseCase, GetMyProfileUseCase>();
        services.AddScoped<IUpdateTenantProfileUseCase, UpdateTenantProfileUseCase>();
        services.AddScoped<IUpdateLandlordProfileUseCase, UpdateLandlordProfileUseCase>();
        services.AddScoped<IUpdateAdminProfileUseCase, UpdateAdminProfileUseCase>();
        services.AddScoped<IVerifyLandlordKycUseCase, VerifyLandlordKycUseCase>();
        services.AddScoped<IGetPublicProfileUseCase, GetPublicProfileUseCase>();
        services.AddScoped<IGetPendingLandlordsUseCase, GetPendingLandlordsUseCase>();
        services.AddScoped<IGetLandlordKycDetailUseCase, GetLandlordKycDetailUseCase>();
        services.AddScoped<ICreateKycUploadUrlUseCase, CreateKycUploadUrlUseCase>();

        return services;
    }
}
