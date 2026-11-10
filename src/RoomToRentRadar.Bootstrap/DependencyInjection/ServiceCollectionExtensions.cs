using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;
using RoomToRentRadar.Adapters.Outbound.Authentication;
using RoomToRentRadar.Adapters.Outbound.Persistence;
using RoomToRentRadar.Adapters.Outbound.Persistence.Repositories;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Authentication;

namespace RoomToRentRadar.Bootstrap.DependencyInjection;

/// <summary>
/// Composition root registrations connecting Application ports to Inbound and Outbound adapters.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationUseCases(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IRegisterUserUseCase, RegisterUserUseCase>();
        services.AddScoped<ILoginWithEmailUseCase, LoginWithEmailUseCase>();
        services.AddScoped<ILoginWithGoogleUseCase, LoginWithGoogleUseCase>();
        return services;
    }

    public static IServiceCollection AddOutboundAdapters(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<GoogleAuthOptions>()
            .Bind(configuration.GetSection(GoogleAuthOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=roomtorentradar.db";

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(
                connectionString,
                sqlite => sqlite.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IGoogleIdentityVerifier, GoogleIdentityVerifier>();

        return services;
    }

    public static IServiceCollection AddInboundAdapters(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services
            .AddControllers()
            .AddApplicationPart(typeof(HealthController).Assembly);

        return services;
    }
}
