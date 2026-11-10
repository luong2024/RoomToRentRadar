using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Datastore.CosmosDb.Context;
using RoomToRentRadar.Infrastructure.Adapters.Mocks;
using RoomToRentRadar.Infrastructure.Adapters.Security;
using RoomToRentRadar.Infrastructure.Repositories;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        JwtSettings? jwtSettings = null,
        bool useMockSsoVerifier = true)
    {
        if (jwtSettings != null)
        {
            services.AddSingleton(jwtSettings);
            services.AddSingleton<IJwtTokenServicePort, JwtTokenServiceAdapter>();
        }

        services.AddSingleton<IPasswordHasherPort, Pbkdf2PasswordHasherAdapter>();
        services.AddSingleton<ISecurityAuditLoggerPort, SecurityAuditLoggerAdapter>();

        if (useMockSsoVerifier)
        {
            services.AddSingleton<IIctuSsoVerifierPort, MockIctuSsoVerifierAdapter>();
        }
        else
        {
            services.AddSingleton<IIctuSsoVerifierPort, IctuSsoVerifierAdapter>();
        }

        services.AddSingleton<InMemoryUserRepository>();
        services.AddScoped<IUserRepository>(sp =>
        {
            var cosmosContext = sp.GetService<CosmosDbContext>();
            if (cosmosContext is { IsConfigured: true })
            {
                return new CosmosUserRepository(cosmosContext);
            }

            return sp.GetRequiredService<InMemoryUserRepository>();
        });

        return services;
    }
}
