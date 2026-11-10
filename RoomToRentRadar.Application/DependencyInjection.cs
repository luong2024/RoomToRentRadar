using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Application.UseCases.Auth;
using RoomToRentRadar.Ports.Inbound.UseCases.Auth;

namespace RoomToRentRadar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IRegisterUserUseCase, RegisterUserUseCase>();
        services.AddScoped<ILoginUserUseCase, LoginUserUseCase>();
        services.AddScoped<ILoginWithIctuSsoUseCase, LoginWithIctuSsoUseCase>();
        services.AddScoped<IRefreshTokenUseCase, RefreshTokenUseCase>();
        services.AddScoped<IRevokeTokenUseCase, RevokeTokenUseCase>();
        services.AddScoped<IGetCurrentUserProfileUseCase, GetCurrentUserProfileUseCase>();
        services.AddScoped<IApproveLandlordUseCase, ApproveLandlordUseCase>();

        return services;
    }
}
