using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Adapters.Outbound.Storage;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.RoomGallery;

namespace RoomToRentRadar.Bootstrap.DependencyInjection;

public static class RoomGalleryServiceCollectionExtensions
{
    public static IServiceCollection AddRoomGalleryFeature(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PhotoStorageOptions>(configuration.GetSection(PhotoStorageOptions.SectionName));

        var storageOptions = configuration
            .GetSection(PhotoStorageOptions.SectionName)
            .Get<PhotoStorageOptions>() ?? new PhotoStorageOptions();

        if (storageOptions.IsCloudflareR2Configured())
        {
            services.AddHttpClient<CloudflareR2PhotoStorageService>();
            services.AddScoped<IRoomPhotoStorageService>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient(nameof(CloudflareR2PhotoStorageService));
                var options = sp.GetRequiredService<IOptions<PhotoStorageOptions>>();
                return new CloudflareR2PhotoStorageService(httpClient, options, sp.GetService<TimeProvider>());
            });
        }
        else
        {
            services.AddSingleton<IRoomPhotoStorageService, InMemoryRoomPhotoStorageService>();
        }

        services.AddSingleton<IImageCompressionService, SkiaImageCompressionService>();
        services.AddScoped<IRoomPhotoRepository, CosmosRoomPhotoRepository>();

        services.AddScoped<IGetRoomGalleryUseCase, GetRoomGalleryUseCase>();
        services.AddScoped<IUploadRoomPhotoUseCase, UploadRoomPhotoUseCase>();
        services.AddScoped<IDeleteRoomPhotoUseCase, DeleteRoomPhotoUseCase>();
        services.AddScoped<IReorderRoomPhotosUseCase, ReorderRoomPhotosUseCase>();

        return services;
    }
}
