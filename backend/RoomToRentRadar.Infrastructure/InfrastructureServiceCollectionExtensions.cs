using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoomToRentRadar.Infrastructure.Auth;
using RoomToRentRadar.Infrastructure.BackgroundJobs;
using RoomToRentRadar.Infrastructure.Photos;
using RoomToRentRadar.Infrastructure.Travel;
using RoomToRentRadar.Ports.Models.SavedSearches;
using RoomToRentRadar.Ports.Models.Travel;
using RoomToRentRadar.Ports.Outbound.Services;

namespace RoomToRentRadar.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenGenerator, TokenGenerator>();

        services.AddSingleton(serviceProvider => PhotoStorageOptions.Resolve(
            configuration[PhotoStorageOptions.ConfigKey],
            serviceProvider.GetRequiredService<IHostEnvironment>().ContentRootPath));
        services.AddSingleton<IPhotoStorage, PhotoStorage>();
        services.AddSingleton<IPhotoProcessor, PhotoProcessor>();

        services.Configure<AlertsOptions>(configuration.GetSection(AlertsOptions.SectionName));
        services.Configure<TravelOptions>(configuration.GetSection(TravelOptions.SectionName));

        services
            .AddHttpClient<IRoutingEstimatesProvider, GoogleRoutesEstimatesProvider>(client =>
            {
                client.BaseAddress = new Uri("https://routes.googleapis.com/");
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddStandardResilienceHandler(resilience =>
            {
                resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
                resilience.Retry.MaxRetryAttempts = 1;
            });

        services.AddHostedService<SavedSearchAlertWorker>();

        return services;
    }
}
