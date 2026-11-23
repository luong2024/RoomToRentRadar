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

        var isDevOrTest = environment.IsDevelopment() || environment.IsEnvironment("Testing");

        services
            .AddOptions<PhotoStorageOptions>()
            .Bind(configuration.GetSection(PhotoStorageOptions.SectionName))
            .Validate(
                options => options.IsCloudflareR2Configured() || (options.IsInMemoryProvider() && isDevOrTest),
                $"Invalid PhotoStorage configuration for environment '{environment.EnvironmentName}'. " +
                "Provide valid Cloudflare R2 credentials (AccountId, BucketName, AccessKeyId, SecretAccessKey, PublicBaseUrl), " +
                "or explicitly set PhotoStorage:Provider='InMemory' in Development/Testing.")
            .ValidateOnStart();

        services.AddHttpClient<CloudflareR2PhotoStorageService>();
        services.AddSingleton<InMemoryRoomPhotoStorageService>();
        services.AddScoped<IRoomPhotoStorageService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PhotoStorageOptions>>();
            var storageOptions = options.Value;

            if (storageOptions.IsCloudflareR2Configured())
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient(nameof(CloudflareR2PhotoStorageService));
                return new CloudflareR2PhotoStorageService(httpClient, options, sp.GetService<TimeProvider>());
            }

            if (storageOptions.IsInMemoryProvider() && isDevOrTest)
            {
                return sp.GetRequiredService<InMemoryRoomPhotoStorageService>();
            }

            throw new InvalidOperationException(
                $"Invalid PhotoStorage configuration for environment '{environment.EnvironmentName}'. " +
                "Cloudflare R2 credentials are required outside Development/Testing.");
        });

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
