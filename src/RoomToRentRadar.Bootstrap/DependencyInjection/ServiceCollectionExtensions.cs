using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Adapters.Inbound.WebApi.Authentication;
using RoomToRentRadar.Adapters.Inbound.WebApi.Authorization;
using RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb;
using RoomToRentRadar.Adapters.Inbound.WebApi.Middleware;
using RoomToRentRadar.Adapters.Inbound.WebApi.RateLimiting;
using RoomToRentRadar.Adapters.Outbound.Authentication;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Authentication;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.UseCases.RoomListings;

namespace RoomToRentRadar.Bootstrap.DependencyInjection;

/// <summary>
/// Composition root registrations connecting Application ports to Inbound and Outbound adapters.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationUseCases(this IServiceCollection services)
    {
        services.AddScoped<IGetRoomListingsUseCase, GetRoomListingsUseCase>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IRegisterUserUseCase, RegisterUserUseCase>();
        services.AddScoped<ILoginWithEmailUseCase, LoginWithEmailUseCase>();
        services.AddScoped<ILoginWithGoogleUseCase, LoginWithGoogleUseCase>();
        services.AddScoped<IRefreshSessionUseCase, RefreshSessionUseCase>();
        services.AddScoped<ILogoutUseCase, LogoutUseCase>();
        services.AddScoped<IAccountSecurityValidator, AccountSecurityValidator>();
        return services;
    }

    public static IServiceCollection AddOutboundAdapters(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCachingAdapters();
        services.AddCosmosDbPersistence(configuration);
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<GoogleAuthOptions>()
            .Bind(configuration.GetSection(GoogleAuthOptions.SectionName));

        services
            .AddOptions<CosmosDbOptions>()
            .Bind(configuration.GetSection(CosmosDbOptions.SectionName));

        services.AddSingleton<ICosmosDbContext, CosmosDbContext>();
        services.AddScoped<CosmosUnitOfWork>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CosmosUnitOfWork>());
        services.AddScoped<IUserRepository, CosmosUserRepository>();
        services.AddScoped<IRefreshTokenRepository, CosmosRefreshTokenRepository>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IGoogleIdentityVerifier, GoogleIdentityVerifier>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        return services;
    }

    public static IServiceCollection AddInboundAdapters(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        if (configuration is not null)
        {
            services
                .AddOptions<RateLimitingOptions>()
                .Bind(configuration.GetSection(RateLimitingOptions.SectionName));
        }
        else
        {
            services.AddOptions<RateLimitingOptions>();
        }

        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, JwtBearerOptionsSetup>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.AuthenticatedUser, policy =>
                policy.RequireAuthenticatedUser())
            .AddPolicy(AuthorizationPolicies.StudentOnly, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireRole(AuthorizationPolicies.Roles.Student))
            .AddPolicy(AuthorizationPolicies.LandlordOnly, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireRole(AuthorizationPolicies.Roles.Landlord))
            .AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireRole(AuthorizationPolicies.Roles.Admin));

        services.AddRateLimiter(limiterOptions =>
        {
            limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiterOptions.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Detail = "Rate limit exceeded for authentication requests. Please try again later.",
                    Instance = context.HttpContext.Request.Path
                };

                await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            };

            limiterOptions.AddPolicy(RateLimitingPolicies.Authentication, httpContext =>
            {
                var rateOptions = httpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitingOptions>>()
                    .Value;

                var permitLimit = rateOptions.AuthPermitLimit > 0 ? rateOptions.AuthPermitLimit : 30;
                var windowSeconds = rateOptions.AuthWindowSeconds > 0 ? rateOptions.AuthWindowSeconds : 60;

                var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "local-client";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        services
            .AddControllers()
            .AddApplicationPart(typeof(HealthController).Assembly);

        return services;
    }
}
