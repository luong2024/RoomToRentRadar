using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Application.UseCases.Audit;
using RoomToRentRadar.Application.UseCases.Auth;
using RoomToRentRadar.Application.UseCases.Landlord;
using RoomToRentRadar.Application.UseCases.Photos;
using RoomToRentRadar.Application.UseCases.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Audit;
using RoomToRentRadar.Ports.Inbound.UseCases.Auth;
using RoomToRentRadar.Ports.Inbound.UseCases.Landlord;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;
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
        services.AddScoped<ISearchRoomsUseCase, SearchRoomsUseCase>();

        // US08 / T08.2 — photo upload and landlord listing CRUD (UC-30..UC-33).
        services.AddScoped<IUploadPhotoUseCase, UploadPhotoUseCase>();
        services.AddScoped<IDeletePhotoUseCase, DeletePhotoUseCase>();
        services.AddScoped<ICleanupExpiredPhotosUseCase, CleanupExpiredPhotosUseCase>();
        services.AddScoped<ICreateListingUseCase, CreateListingUseCase>();
        services.AddScoped<IGetMyListingUseCase, GetMyListingUseCase>();
        services.AddScoped<IUpdateListingUseCase, UpdateListingUseCase>();
        services.AddScoped<IListMyListingsUseCase, ListMyListingsUseCase>();
        services.AddScoped<IMarkListingRentedUseCase, MarkListingRentedUseCase>();
        services.AddScoped<IMarkListingAvailableUseCase, MarkListingAvailableUseCase>();
        services.AddScoped<IUnpublishListingUseCase, UnpublishListingUseCase>();
        services.AddScoped<IRepublishListingUseCase, RepublishListingUseCase>();
        services.AddScoped<IResubmitListingUseCase, ResubmitListingUseCase>();

        return services;
    }
}
