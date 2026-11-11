using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomToRentRadar.Infrastructure.Adapters.Mocks;
using RoomToRentRadar.Infrastructure.Adapters.Security;
using RoomToRentRadar.Infrastructure.Adapters.Storage;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        JwtSettings? jwtSettings = null,
        bool useMockSsoVerifier = false)
    {
        if (jwtSettings != null)
        {
            services.AddSingleton(jwtSettings);
            services.AddSingleton<IJwtTokenServicePort, JwtTokenServiceAdapter>();
        }

        services.AddSingleton<IPasswordHasherPort, Pbkdf2PasswordHasherAdapter>();
        services.AddSingleton<ISecurityAuditLoggerPort>(sp =>
            new SecurityAuditLoggerAdapter(scopeFactory: sp.GetService<IServiceScopeFactory>()));

        if (useMockSsoVerifier)
        {
            services.AddSingleton<IIctuSsoVerifierPort, MockIctuSsoVerifierAdapter>();
        }
        else
        {
            services.AddSingleton<IIctuSsoVerifierPort, IctuSsoVerifierAdapter>();
        }

        services.AddSingleton<InMemoryUserRepository>();
        services.TryAddScoped<IUserRepository>(sp => sp.GetRequiredService<InMemoryUserRepository>());
        services.AddSingleton<InMemoryAuditLogRepository>();
        services.TryAddScoped<IAuditLogRepository>(sp => sp.GetRequiredService<InMemoryAuditLogRepository>());

        return services;
    }

    public static IServiceCollection AddPhotoInfrastructure(this IServiceCollection services, string uploadsRootPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(uploadsRootPath))
        {
            throw new ArgumentException("Uploads root path is required.", nameof(uploadsRootPath));
        }

        services.AddSingleton<IPhotoStoragePort>(new LocalDiskPhotoStorageAdapter(uploadsRootPath));
        services.AddSingleton<InMemoryRoomPhotoRepository>();
        services.TryAddScoped<IRoomPhotoRepository>(sp => sp.GetRequiredService<InMemoryRoomPhotoRepository>());

        return services;
    }
}
