using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.Configure<PhotoStorageOptions>(configuration.GetSection(PhotoStorageOptions.SectionName));

        var storageOptions = configuration
            .GetSection(PhotoStorageOptions.SectionName)
            .Get<PhotoStorageOptions>() ?? new PhotoStorageOptions();

        var isDevOrTest = environment.IsDevelopment() || environment.IsEnvironment("Testing");

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
        else if (storageOptions.IsInMemoryProvider() && isDevOrTest)
        {
            services.AddSingleton<IRoomPhotoStorageService, InMemoryRoomPhotoStorageService>();
        }
        else if (storageOptions.IsInMemoryProvider())
        {
            throw new InvalidOperationException(
                $"In-memory photo storage ('PhotoStorage:Provider=InMemory') is only permitted in Development or Testing environments, not '{environment.EnvironmentName}'. Configure valid Cloudflare R2 credentials.");
        }
        else
        {
            throw new InvalidOperationException(
                $"Cloudflare R2 photo storage is not fully configured for environment '{environment.EnvironmentName}'. " +
                "Provide PhotoStorage:AccountId, BucketName, AccessKeyId, SecretAccessKey, and PublicBaseUrl " +
                "(or explicitly set PhotoStorage:Provider='InMemory' in Development/Testing).");
        }

        services.AddSingleton<IImageCompressionService, SkiaImageCompressionService>();
        services.AddScoped<IRoomPhotoRepository, CosmosRoomPhotoRepository>();

        services.AddScoped<IGetRoomGalleryUseCase, GetRoomGalleryUseCase>();
        services.AddScoped<IUploadRoomPhotoUseCase, UploadRoomPhotoUseCase>();
        services.AddScoped<IDeleteRoomPhotoUseCase, DeleteRoomPhotoUseCase>();
        services.AddScoped<IReorderRoomPhotosUseCase, ReorderRoomPhotosUseCase>();
        services.AddScoped<IAssignRoomGalleryOwnerUseCase, AssignRoomGalleryOwnerUseCase>();

        return services;
    }
}
