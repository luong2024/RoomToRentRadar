using Microsoft.Extensions.FileProviders;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services.Photos;

namespace RoomToRentRadar.Api;

public static class PhotoPipelineExtensions
{
    public static IServiceCollection AddPhotoPipeline(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<PhotoOptions>(config.GetSection(PhotoOptions.SectionName));
        services.AddSingleton<IPhotoStorage, LocalPhotoStorage>();
        services.AddSingleton<IImageProcessor, ImageSharpProcessor>();
        services.AddSingleton<IPhotoRepository, InMemoryPhotoRepository>();
        services.AddScoped<IPhotoService, PhotoService>();
        return services;
    }

    public static WebApplication UsePhotoStaticFiles(this WebApplication app)
    {
        var options = app.Configuration.GetSection(PhotoOptions.SectionName).Get<PhotoOptions>() ?? new PhotoOptions();
        string root = LocalPhotoStorage.ResolveRoot(options, app.Environment.ContentRootPath);
        Directory.CreateDirectory(root);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(root),
            RequestPath = LocalPhotoStorage.RequestPath
        });
        return app;
    }
}
