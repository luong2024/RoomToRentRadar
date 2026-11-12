using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomToRentRadar.Infrastructure.Adapters.Mocks;
using RoomToRentRadar.Infrastructure.Adapters.Security;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Ports.Outbound.Security;

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

        // TODO [US02/T02.2 - Cosmos/Sqlite Migration]: Replace InMemoryRoomRepository with production adapter when datastore slice is implemented
        services.AddSingleton<InMemoryRoomRepository>();
        services.TryAddScoped<IRoomRepository>(sp => sp.GetRequiredService<InMemoryRoomRepository>());
        services.TryAddScoped<IRoomSearchReadModel>(sp => sp.GetRequiredService<InMemoryRoomRepository>());
        services.TryAddScoped<ICurrentUserContextPort, InMemoryCurrentUserContext>();

        return services;
    }
}
