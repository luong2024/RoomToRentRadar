using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Application.UseCases.Audit;
using RoomToRentRadar.Application.UseCases.Auth;
using RoomToRentRadar.Application.UseCases.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Audit;
using RoomToRentRadar.Ports.Inbound.UseCases.Auth;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;

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
        services.AddScoped<IGetAuditLogsUseCase, GetAuditLogsUseCase>();
        services.AddScoped<IGetRoomDetailUseCase, GetRoomDetailUseCase>();
        services.AddScoped<IGetSimilarRoomsUseCase, GetSimilarRoomsUseCase>();

        return services;
    }
}
