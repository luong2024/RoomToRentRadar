using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomToRentRadar.Infrastructure.Adapters.Media;
using RoomToRentRadar.Infrastructure.Adapters.Mocks;
using RoomToRentRadar.Infrastructure.Adapters.Routing;
using RoomToRentRadar.Infrastructure.Adapters.Security;
using RoomToRentRadar.Infrastructure.Adapters.Time;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        JwtSettings? jwtSettings = null,
        bool useMockSsoVerifier = false,
        MediaSettings? mediaSettings = null,
        bool useMockRouting = false,
        RoutingSettings? routingSettings = null)
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

        // ADR-008: media files live on the API host's disk behind the two media ports.
        var effectiveMediaSettings = mediaSettings ?? new MediaSettings();
        services.AddSingleton(effectiveMediaSettings);
        services.AddSingleton<IPhotoStoragePort, LocalDiskPhotoStorageAdapter>();
        services.AddSingleton<IImageProcessorPort, ByteLevelImageProcessorAdapter>();

        // ADR-007 / US09: clock & OpenRouteService routing (mock off by default per GUARDRAILS §1.5.4).
        services.TryAddSingleton<IClock, SystemClock>();
        var effectiveRoutingSettings = routingSettings ?? new RoutingSettings();
        services.AddSingleton(effectiveRoutingSettings);
        if (useMockRouting)
        {
            services.TryAddSingleton<ITravelTimePort, MockTravelTimeAdapter>();
        }
        else
        {
            services.TryAddSingleton<ITravelTimePort>(sp =>
                new OpenRouteServiceTravelTimeAdapter(new HttpClient(), sp.GetRequiredService<RoutingSettings>()));
        }

        // No IRoomRepository fallback: SQLite is the only datastore (ADR-002).
        // InMemoryRoomRepository still lives in this (production) assembly but is deliberately NOT
        // registered here; only tests construct it directly.

        return services;
    }
}
